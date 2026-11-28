using System.IO;
using System.Reflection;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Controller;
using SunshineHelper.Application.Devices;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Monitoring;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Monitoring;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Channel;
using SunshineHelper.Infrastructure.CloseGame;
using SunshineHelper.Infrastructure.Controller;
using SunshineHelper.Infrastructure.Diagnostics;
using SunshineHelper.Infrastructure.Display;
using SunshineHelper.Infrastructure.Fps;
using SunshineHelper.Infrastructure.Launcher;
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
    private readonly IDiagnosticLog _diagnosticLog;
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private CancellationTokenSource? _monitorStopping;
    private Task? _monitorTask;
    private Task? _watchdogTask;
    private Task? _controllerTask;

    private AppHost(
        CurrentSettingsHolder settings,
        SessionStatusReader status,
        IReadOnlyList<string> startupProblems,
        TriggerChannelHandler handler,
        NamedPipeChannelServer pipeServer,
        SettingsService settingsService,
        ISessionSnapshotStore store,
        IDiagnosticLog diagnosticLog,
        DeviceRegistry devices)
    {
        Settings = settings;
        Status = status;
        StartupProblems = startupProblems;
        _handler = handler;
        _pipeServer = pipeServer;
        _settingsService = settingsService;
        _store = store;
        _diagnosticLog = diagnosticLog;
        Devices = devices;
    }

    public CurrentSettingsHolder Settings { get; }

    /// <summary>The devices that have connected, for the Devices screen.</summary>
    public DeviceRegistry Devices { get; }

    public SessionStatusReader Status { get; }

    /// <summary>The controller reader, shared by the watcher and the Controller screen.</summary>
    public IGamepadReader GamepadReader { get; } = new XInputGamepadReader();

    /// <summary>Why settings.json was ignored in favour of defaults, or why a leftover
    /// session could not be put back. Empty when everything went well.</summary>
    public IReadOnlyList<string> StartupProblems { get; }

    /// <summary>False until the user sets the Sunshine log path; the log reader (resume
    /// and plain disconnect) is off until then, the pipe works regardless.</summary>
    public bool LogMonitorRunning => _monitorTask is not null;

    /// <summary>What the Sunshine log level looks like, judged when the log reader starts.
    /// <see cref="LogLevelVerdict.Insufficient"/> means /cancel and /resume would never
    /// show up; the app only says so, it never edits Sunshine's configuration.</summary>
    public LogLevelVerdict LogLevel { get; private set; } = LogLevelVerdict.Unknown;

    /// <summary>Three numbers on purpose: the fourth one made "1.0.0.0" look like an IP
    /// address to the redactor.</summary>
    public static string AppVersion { get; } = ReadVersion();

    /// <summary>The informational version keeps the "-preview.2" suffix that the numeric
    /// assembly version drops; the "+commit" build metadata the SDK appends is cut off.</summary>
    private static string ReadVersion()
    {
        var assembly = typeof(AppHost).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunshineHelper",
        "settings.json");

    public static string DevicesPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunshineHelper",
        "devices.json");

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
        var diagnosticLog = new FileDiagnosticLog(FileDiagnosticLog.DefaultPath);
        diagnosticLog.Write(
            DiagnosticLevel.Info,
            "app",
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"started: version {AppVersion}, OS {Environment.OSVersion.Version}, {loadedSettings.Profiles.Count} profile(s), log path set: {!string.IsNullOrWhiteSpace(loadedSettings.SunshineLogPath)}"));
        foreach (string problem in problems)
        {
            diagnosticLog.Write(DiagnosticLevel.Warning, "app", "startup problem: " + problem);
        }

        var store = new FileSessionSnapshotStore();
        var orchestrator = new SessionOrchestrator(
            [
                new PrimaryDisplayModule(diagnosticLog),
                new HdrModule(),
                new RtssFpsLimitModule(),
                new LauncherModule(new WindowsProcessController(), settings),
            ],
            store,
            new UptimeBootIdentityProvider(),
            diagnosticLog);

        // A session left behind by a crash or a reboot is put back before anything can
        // start a new one. The status reader then shows whatever could not be undone.
        var restore = await orchestrator.RestoreAsync(cancellationToken).ConfigureAwait(false);
        problems.AddRange(restore.Problems);

        // Only for the Devices screen; a missing or broken devices.json just starts a new list.
        var devices = new DeviceRegistry(new FileSettingsFileStore(), DevicesPath, TimeProvider.System, diagnosticLog);
        await devices.LoadAsync(cancellationToken).ConfigureAwait(false);

        var handler = new TriggerChannelHandler(orchestrator, settings, diagnosticLog, devices);
        var pipeServer = new NamedPipeChannelServer(handler);
        var host = new AppHost(settings, new SessionStatusReader(store), problems, handler, pipeServer, settingsService, store, diagnosticLog, devices);

        pipeServer.Start();
        host.StartLogMonitor();
        host.StartWatchdog();
        host.StartControllerWatcher();
        return host;
    }

    /// <summary>
    /// Builds the text of a diagnostic report from what is on disk right now (settings.json,
    /// app.log, the Sunshine log) and the description the user typed. Already redacted; it
    /// only returns the text, writing it to a file is the caller's job.
    /// </summary>
    public string BuildDiagnosticReport(string? description)
    {
        string? sunshineLogPath = Settings.Current.SunshineLogPath;
        return DiagnosticReportBuilder.Build(new DiagnosticReportInput
        {
            GeneratedAt = DateTimeOffset.Now,
            AppVersion = AppVersion,
            OsVersion = Environment.OSVersion.Version.ToString(),
            Description = description,
            SettingsJson = DiagnosticFiles.ReadText(SettingsPath),
            StartupProblems = StartupProblems,
            AppLogLines = DiagnosticFiles.ReadLastLines(FileDiagnosticLog.DefaultPath, DiagnosticReportBuilder.AppLogLineLimit),
            SunshineLogPath = sunshineLogPath,
            SunshineLogLines = string.IsNullOrWhiteSpace(sunshineLogPath)
                ? null
                : DiagnosticFiles.ReadLastLines(sunshineLogPath, DiagnosticReportBuilder.SunshineLogLineLimit),
        });
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
        if (_watchdogTask is not null)
        {
            await _watchdogTask.ConfigureAwait(false);
        }

        if (_controllerTask is not null)
        {
            await _controllerTask.ConfigureAwait(false);
        }

        await _pipeServer.DisposeAsync().ConfigureAwait(false);
        _handler.Dispose();
        Devices.Dispose();
        _updateGate.Dispose();
        _stopping.Dispose();
    }

    /// <summary>Restores the environment by itself when Sunshine stops in the middle of a
    /// session (docs/adr/0013-sunshine-encerrado-restaura.md). Runs whether or not the log
    /// path is set: it only looks at the snapshot and the process list.</summary>
    private void StartWatchdog()
    {
        var watchdog = new SunshineProcessWatchdog(new SunshineProcessProbe(), _handler, _store, TimeProvider.System, _diagnosticLog);
        var token = _stopping.Token;
        _watchdogTask = Task.Run(() => watchdog.RunAsync(SunshineProcessWatchdog.DefaultInterval, token), CancellationToken.None);
    }

    /// <summary>Reads the controllers during a session and runs the action behind each combo
    /// the user turned on (docs/adr/0015-overlay-pelo-controle.md). Today that is closing the
    /// game in the foreground (docs/adr/0016-fechar-jogo-pelo-controle.md); the overlay joins
    /// later. Outside a session, or with no combo turned on, it does nothing but look once a
    /// second.</summary>
    private void StartControllerWatcher()
    {
        var watcher = new ControllerWatcher(GamepadReader, _store, Settings, TimeProvider.System, _diagnosticLog);
        var closeGame = new CloseGameService(
            new WindowsGameWindowController(),
            new WindowsProcessController(),
            Settings,
            Status,
            TimeProvider.System,
            _diagnosticLog,
            new WindowsElevatedGameCloser());
        var token = _stopping.Token;

        // The watcher's loop must keep reading the controller while a game is being closed
        // (the wait for the game can take seconds), so the action runs on its own task; the
        // service lets only one round run at a time.
        watcher.ComboFired += (combo, slot) =>
        {
            if (string.Equals(combo.Id, ControllerSettings.CloseGameComboId, StringComparison.Ordinal))
            {
                _ = Task.Run(() => RunCloseGameAsync(closeGame, token), CancellationToken.None);
            }
        };
        watcher.SessionEnded += closeGame.Reset;

        _controllerTask = Task.Run(() => watcher.RunAsync(token), CancellationToken.None);
    }

    private async Task RunCloseGameAsync(CloseGameService closeGame, CancellationToken cancellationToken)
    {
        try
        {
            await closeGame.HandleComboAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _diagnosticLog.Write(DiagnosticLevel.Error, "closeGame", "failed: " + ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is shutting down.
        }
    }

    private void StartLogMonitor()
    {
        var current = Settings.Current;
        if (string.IsNullOrWhiteSpace(current.SunshineLogPath))
        {
            return;
        }

        LogLevel = SunshineLogLevelCheck.Evaluate(SunshineLogSampler.Read(current.SunshineLogPath));

        _monitorStopping = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        var token = _monitorStopping.Token;

        var source = new SunshineLogFileSource(current.SunshineLogPath, current.Timing.LogPollIntervalMs);
        var monitor = new SunshineSessionMonitor(source, _handler, _store, Settings, _diagnosticLog);
        _monitorTask = Task.Run(() => monitor.RunAsync(token), CancellationToken.None);
    }

    private async Task StopLogMonitorAsync()
    {
        LogLevel = LogLevelVerdict.Unknown;
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
