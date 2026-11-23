using System.IO;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Monitoring;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Channel;
using SunshineHelper.Infrastructure.Display;
using SunshineHelper.Infrastructure.Fps;
using SunshineHelper.Infrastructure.Monitoring;
using SunshineHelper.Infrastructure.Session;
using SunshineHelper.Infrastructure.Settings;

namespace SunshineHelper.App.Services;

/// <summary>
/// The composition root: builds the real modules, orchestrator, pipe channel and log
/// monitor and keeps them running for the life of the app (docs/adr/0006-hospedagem-do-app.md).
/// Order matters at startup: settings first, then recovery of a session a previous run
/// left behind, and only then the pipe — so a client connecting during recovery never
/// races with it.
/// </summary>
internal sealed class AppHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TriggerChannelHandler _handler;
    private readonly NamedPipeChannelServer _pipeServer;
    private Task? _monitorTask;

    private AppHost(
        CurrentSettingsHolder settings,
        SessionStatusReader status,
        IReadOnlyList<string> startupProblems,
        TriggerChannelHandler handler,
        NamedPipeChannelServer pipeServer)
    {
        Settings = settings;
        Status = status;
        StartupProblems = startupProblems;
        _handler = handler;
        _pipeServer = pipeServer;
    }

    public CurrentSettingsHolder Settings { get; }

    public SessionStatusReader Status { get; }

    /// <summary>Why settings.json was ignored in favour of defaults, or why a leftover
    /// session could not be put back. Empty when everything went well.</summary>
    public IReadOnlyList<string> StartupProblems { get; }

    /// <summary>False until the user sets the Sunshine log path; the log reader (resume
    /// and plain disconnect) is off until then, the pipe works regardless.</summary>
    public bool LogMonitorRunning => _monitorTask is not null;

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunshineHelper",
        "settings.json");

    public static async Task<AppHost> StartAsync(CancellationToken cancellationToken)
    {
        List<string> problems = [];

        // A broken or unreadable settings.json must not stop the app: it runs with no
        // profiles (so nothing is applied) and says why.
        var loadedSettings = AppSettings.CreateDefault();
        try
        {
            var loaded = await new SettingsService(new FileSettingsFileStore())
                .LoadAsync(SettingsPath, cancellationToken).ConfigureAwait(false);
            if (loaded.Validation.IsValid)
            {
                loadedSettings = loaded.Settings;
            }
            else
            {
                problems.AddRange(loaded.Validation.Errors);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Não foi possível ler settings.json: {ex.Message}");
        }

        var settings = new CurrentSettingsHolder(loadedSettings);
        var store = new FileSessionSnapshotStore();
        var orchestrator = new SessionOrchestrator(
            [new PrimaryDisplayModule(), new HdrModule(), new RtssFpsLimitModule()],
            store,
            new UptimeBootIdentityProvider());

        // A session left behind by a crash or a reboot is put back before anything can
        // start a new one. The status reader then shows whatever could not be undone.
        var restore = await orchestrator.RestoreAsync(cancellationToken).ConfigureAwait(false);
        problems.AddRange(restore.Problems);

        var handler = new TriggerChannelHandler(orchestrator, settings);
        var pipeServer = new NamedPipeChannelServer(handler);
        var host = new AppHost(settings, new SessionStatusReader(store), problems, handler, pipeServer);

        pipeServer.Start();
        host.StartLogMonitor(store);
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: the token above is what stopped it.
            }
        }

        await _pipeServer.DisposeAsync().ConfigureAwait(false);
        _handler.Dispose();
        _stopping.Dispose();
    }

    private void StartLogMonitor(ISessionSnapshotStore store)
    {
        var current = Settings.Current;
        if (string.IsNullOrWhiteSpace(current.SunshineLogPath))
        {
            return;
        }

        var source = new SunshineLogFileSource(current.SunshineLogPath, current.Timing.LogPollIntervalMs);
        var monitor = new SunshineSessionMonitor(source, _handler, store, Settings);
        _monitorTask = Task.Run(() => monitor.RunAsync(_stopping.Token), CancellationToken.None);
    }
}
