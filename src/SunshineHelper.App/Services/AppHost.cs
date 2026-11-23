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
    private readonly SettingsService _settingsService;
    private readonly ISessionSnapshotStore _store;
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private CancellationTokenSource? _monitorStopping;
    private Task? _monitorTask;

    private AppHost(
        CurrentSettingsHolder settings,
        SessionStatusReader status,
        IReadOnlyList<string> startupProblems,
        TriggerChannelHandler handler,
        NamedPipeChannelServer pipeServer,
        SettingsService settingsService,
        ISessionSnapshotStore store)
    {
        Settings = settings;
        Status = status;
        StartupProblems = startupProblems;
        _handler = handler;
        _pipeServer = pipeServer;
        _settingsService = settingsService;
        _store = store;
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
        var settingsService = new SettingsService(new FileSettingsFileStore());
        try
        {
            var loaded = await settingsService
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
        var host = new AppHost(settings, new SessionStatusReader(store), problems, handler, pipeServer, settingsService, store);

        pipeServer.Start();
        host.StartLogMonitor();
        return host;
    }

    /// <summary>
    /// Saves a settings change to settings.json and puts it into use: the pipe handler and
    /// the log monitor read the new values from then on, and the log reader is restarted
    /// when its path changed. Serialized, so two quick edits never interleave. Throws
    /// <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> when the file
    /// cannot be written (nothing is applied then) and
    /// <see cref="InvalidOperationException"/> when the result would be invalid.
    /// </summary>
    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change, CancellationToken cancellationToken)
    {
        await _updateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = Settings.Current;
            var updated = change(previous);

            var validation = SettingsValidator.Validate(updated);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join(' ', validation.Errors));
            }

            // Saved before it is used: a failed write must not leave the app running with
            // settings that the next start would not find.
            await _settingsService.SaveAsync(SettingsPath, updated, cancellationToken).ConfigureAwait(false);
            Settings.Publish(updated);

            if (!string.Equals(previous.SunshineLogPath, updated.SunshineLogPath, StringComparison.Ordinal))
            {
                await StopLogMonitorAsync().ConfigureAwait(false);
                StartLogMonitor();
            }
        }
        finally
        {
            _updateGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        await StopLogMonitorAsync().ConfigureAwait(false);

        await _pipeServer.DisposeAsync().ConfigureAwait(false);
        _handler.Dispose();
        _updateGate.Dispose();
        _stopping.Dispose();
    }

    private void StartLogMonitor()
    {
        var current = Settings.Current;
        if (string.IsNullOrWhiteSpace(current.SunshineLogPath))
        {
            return;
        }

        _monitorStopping = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        var token = _monitorStopping.Token;

        var source = new SunshineLogFileSource(current.SunshineLogPath, current.Timing.LogPollIntervalMs);
        var monitor = new SunshineSessionMonitor(source, _handler, _store, Settings);
        _monitorTask = Task.Run(() => monitor.RunAsync(token), CancellationToken.None);
    }

    private async Task StopLogMonitorAsync()
    {
        if (_monitorStopping is null || _monitorTask is null)
        {
            return;
        }

        await _monitorStopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _monitorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: the token above is what stopped it.
        }

        _monitorStopping.Dispose();
        _monitorStopping = null;
        _monitorTask = null;
    }
}
