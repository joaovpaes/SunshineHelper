using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.CloseGame;

/// <summary>
/// Closes the game in the foreground when the close-game combo fires, without ever reaching
/// another program (docs/adr/0016-fechar-jogo-pelo-controle.md).
/// </summary>
/// <remarks>
/// <para><b>Target.</b> The process of the foreground window at the moment of the hold,
/// kept by PID and start time. It is refused when it is protected
/// (<see cref="CloseGameProtection"/>): Windows, Sunshine, this app, the launcher, the
/// user's list, or anything that was running before the stream began.</para>
/// <para><b>Closing.</b> First a polite request (like clicking the X), then a wait of
/// <see cref="CloseGameSettings.PoliteTimeoutMs"/>. If the game is still there, it is not
/// ended on its own: holding the combo again within <see cref="ConfirmWindow"/> ends
/// <em>that same process</em>, wherever the focus went in the meantime. A second hold never
/// picks a new target. <see cref="CloseGameSettings.ForceWithoutConfirm"/> skips the second
/// hold.</para>
/// <para><b>Not leaking to the next program.</b> After every round that closed, ended or
/// failed to end the game, the combo is ignored for <see cref="Cooldown"/>, so the launcher
/// that comes to the front cannot be hit by a quick second hold.</para>
/// <para>One round at a time: a hold that arrives while the previous one is still waiting
/// for the game gets <see cref="CloseGameResult.Busy"/>.</para>
/// <para><b>Games that run as administrator.</b> Windows does not let this app (which is not
/// elevated) close or end them. When the user allowed it on the General screen, the same steps go
/// through the scheduled task (<see cref="IElevatedGameCloser"/>,
/// docs/adr/0017-fechar-jogo-de-administrador.md). Every protection above is still checked here
/// first, and the task checks its own rules again before acting. Ending such a game ends the game
/// alone, not the programs it started.</para>
/// </remarks>
public sealed class CloseGameService
{
    /// <summary>How long a second hold still confirms ending the game that did not close.</summary>
    public static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(15);

    /// <summary>How long the combo is ignored after a round ends.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private const string Area = "closeGame";

    /// <summary>How long the app looks for an ended game that runs as administrator to be gone
    /// (the task already waits for it before answering).</summary>
    private static readonly TimeSpan _elevatedKillCheck = TimeSpan.FromSeconds(3);

    private const string AllowHint = "on the General screen, use 'Allow closing administrator games'";

    private readonly IGameWindowController _windows;
    private readonly IProcessController _processes;
    private readonly ICurrentSettingsProvider _settings;
    private readonly SessionStatusReader _session;
    private readonly TimeProvider _time;
    private readonly IDiagnosticLog _log;
    private readonly IElevatedGameCloser _elevated;
    private readonly Lock _gate = new();

    private int _busy;
    private Target? _pending;
    private DateTimeOffset _pendingUntil;
    private DateTimeOffset _cooldownUntil;

    public CloseGameService(
        IGameWindowController windows,
        IProcessController processes,
        ICurrentSettingsProvider settings,
        SessionStatusReader session,
        TimeProvider? timeProvider = null,
        IDiagnosticLog? log = null,
        IElevatedGameCloser? elevated = null)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(session);

        _windows = windows;
        _processes = processes;
        _settings = settings;
        _session = session;
        _time = timeProvider ?? TimeProvider.System;
        _log = log ?? NullDiagnosticLog.Instance;
        _elevated = elevated ?? NoElevatedGameCloser.Instance;
    }

    /// <summary>The game waiting for a second hold, if any (for the overlay to show
    /// "hold again to end it" later).</summary>
    public ProcessIdentity? PendingConfirmation
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null && _time.GetUtcNow() < _pendingUntil ? _pending.Process : null;
            }
        }
    }

    /// <summary>Runs one round for a hold of the close-game combo.</summary>
    public async Task<CloseGameOutcome> HandleComboAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            _log.Write(DiagnosticLevel.Info, Area, "combo ignored: still waiting for the previous game to close");
            return new CloseGameOutcome(CloseGameResult.Busy);
        }

        try
        {
            return await RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    /// <summary>Forgets a pending confirmation and the pause (the session ended).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _pending = null;
            _pendingUntil = default;
            _cooldownUntil = default;
        }
    }

    private async Task<CloseGameOutcome> RunAsync(CancellationToken cancellationToken)
    {
        AppSettings settings = _settings.Current;
        CloseGameSettings closeGame = settings.Controller.CloseGame;
        if (!closeGame.Enabled)
        {
            return new CloseGameOutcome(CloseGameResult.Disabled);
        }

        DateTimeOffset now = _time.GetUtcNow();
        Target? pending;
        lock (_gate)
        {
            if (now < _cooldownUntil)
            {
                _log.Write(DiagnosticLevel.Info, Area, "combo ignored: pause after the last round");
                return new CloseGameOutcome(CloseGameResult.CoolingDown);
            }

            pending = _pending;
            if (pending is not null && now >= _pendingUntil)
            {
                _log.Write(DiagnosticLevel.Info, Area, "the confirmation to end '" + pending.Process.Name + "' expired; starting over");
                pending = null;
            }

            _pending = null;
        }

        if (pending is not null)
        {
            return await ForceAsync(pending, settings, cancellationToken).ConfigureAwait(false);
        }

        SessionStatus status = await _session.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (status.Kind != SessionStatusKind.Active || status.Since is not { } sessionStartedAt)
        {
            _log.Write(DiagnosticLevel.Info, Area, "combo ignored: no stream is on");
            return new CloseGameOutcome(CloseGameResult.NoSession);
        }

        ForegroundWindow foreground = _windows.GetForeground();
        if (foreground.Status == ForegroundStatus.NotInspectable)
        {
            _log.Write(
                DiagnosticLevel.Warning,
                Area,
                "the program in the foreground could not be identified; nothing was closed");
            return new CloseGameOutcome(CloseGameResult.NotInspectable);
        }

        if (foreground.Status is not (ForegroundStatus.Found or ForegroundStatus.Elevated) || foreground.Process is not { } process)
        {
            _log.Write(DiagnosticLevel.Info, Area, "combo ignored: no window in the foreground");
            return new CloseGameOutcome(CloseGameResult.NoTarget);
        }

        var target = new Target(process, foreground.Status == ForegroundStatus.Elevated);
        nint window = foreground.Window;
        CloseGameProtectionReason reason = CloseGameProtection.Check(process, sessionStartedAt, settings);
        if (reason == CloseGameProtectionReason.Launcher)
        {
            // Some launchers (Playnite in fullscreen) keep the foreground while the game
            // they started is on screen. The launcher itself stays untouchable; the target
            // becomes the one game it started during the stream, or nothing.
            (Target? game, nint gameWindow, CloseGameOutcome? refused) = FindLauncherGame(process, sessionStartedAt, settings);
            if (game is null)
            {
                return refused!;
            }

            target = game;
            window = gameWindow;
        }
        else if (reason != CloseGameProtectionReason.None)
        {
            _log.Write(DiagnosticLevel.Info, Area, "'" + process.Name + "' is protected (" + Describe(reason) + "); nothing was closed");
            return new CloseGameOutcome(CloseGameResult.Protected, process.Name, reason);
        }

        if (target.Elevated)
        {
            return await CloseElevatedAsync(target, settings, cancellationToken).ConfigureAwait(false);
        }

        WindowCloseResult request = _windows.RequestCloseWindow(target.Process, window);
        if (request == WindowCloseResult.Denied)
        {
            // Windows refused the message, as it does for a window of a program that runs as
            // administrator: the same request goes through the task, if the user allowed it.
            return await CloseElevatedAsync(target with { Elevated = true }, settings, cancellationToken).ConfigureAwait(false);
        }

        if (request == WindowCloseResult.Gone)
        {
            return Finish(CloseGameResult.Closed, target.Process, settings, "'" + target.Process.Name + "' was already gone");
        }

        return await WaitAfterRequestAsync(target, settings, "asked '" + target.Process.Name + "' to close", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asks a game that runs as administrator to close, through the task.</summary>
    private async Task<CloseGameOutcome> CloseElevatedAsync(Target target, AppSettings settings, CancellationToken cancellationToken)
    {
        ElevatedCloseAttempt attempt = await _elevated.RunAsync(ElevatedCloseOperation.Close, target.Process, cancellationToken).ConfigureAwait(false);
        TaskAnswer answer = await ReadAnswerAsync(attempt, target, "close", cancellationToken).ConfigureAwait(false);
        return answer switch
        {
            TaskAnswer.Gone => Finish(CloseGameResult.Closed, target.Process, settings, "'" + target.Process.Name + "' was already gone"),
            TaskAnswer.Refused => new CloseGameOutcome(CloseGameResult.AccessDenied, target.Process.Name),
            _ => await WaitAfterRequestAsync(
                target,
                settings,
                "asked '" + target.Process.Name + "' (runs as administrator) to close through the scheduled task",
                cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>After the close request reached the game: waits for it, then ends it, or keeps
    /// it waiting for a second hold.</summary>
    private async Task<CloseGameOutcome> WaitAfterRequestAsync(Target target, AppSettings settings, string asked, CancellationToken cancellationToken)
    {
        CloseGameSettings closeGame = settings.Controller.CloseGame;
        TimeSpan timeout = TimeSpan.FromMilliseconds(closeGame.PoliteTimeoutMs);
        string timeoutText = closeGame.PoliteTimeoutMs.ToString(CultureInfo.InvariantCulture);
        _log.Write(DiagnosticLevel.Info, Area, asked + "; waiting up to " + timeoutText + " ms");

        bool exited = await WaitForExitAsync(target, timeout, cancellationToken).ConfigureAwait(false);
        if (exited)
        {
            return Finish(CloseGameResult.Closed, target.Process, settings, "'" + target.Process.Name + "' closed");
        }

        if (closeGame.ForceWithoutConfirm)
        {
            return await ForceAsync(target, settings, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _pending = target;
            _pendingUntil = _time.GetUtcNow() + ConfirmWindow;
        }

        string windowText = ConfirmWindow.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        _log.Write(
            DiagnosticLevel.Info,
            Area,
            "'" + target.Process.Name + "' did not close; hold the combo again within " + windowText + " s to end it");
        return new CloseGameOutcome(CloseGameResult.AwaitingConfirmation, target.Process.Name);
    }

    private Task<bool> WaitForExitAsync(Target target, TimeSpan timeout, CancellationToken cancellationToken) =>
        target.Elevated
            ? _elevated.WaitForExitAsync(target.Process, timeout, cancellationToken)
            : _processes.WaitForExitAsync(target.Process, timeout, cancellationToken);

    /// <summary>Turns the task's answer into what the round does next, writing the reason to the
    /// log when it did not act.</summary>
    private async Task<TaskAnswer> ReadAnswerAsync(ElevatedCloseAttempt attempt, Target target, string verb, CancellationToken cancellationToken)
    {
        string name = target.Process.Name;
        switch (attempt.Status)
        {
            case ElevatedCloseStatus.NotAllowed:
                _log.Write(
                    DiagnosticLevel.Warning,
                    Area,
                    "'" + name + "' runs as administrator, and closing such games is not allowed yet; " + AllowHint);
                return TaskAnswer.Refused;

            case ElevatedCloseStatus.Failed:
                _log.Write(
                    DiagnosticLevel.Warning,
                    Area,
                    "could not start the scheduled task to " + verb + " '" + name + "' (runs as administrator); if it keeps failing, " + AllowHint + " again");
                return TaskAnswer.Refused;

            case ElevatedCloseStatus.Unknown:
                _log.Write(DiagnosticLevel.Info, Area, "the scheduled task to " + verb + " '" + name + "' was started, but its answer was not read in time");
                return TaskAnswer.Proceed;
        }

        int code = attempt.ExitCode ?? (int)ElevatedCloseReason.UnexpectedError;
        if (code == (int)ElevatedCloseReason.Done)
        {
            return TaskAnswer.Proceed;
        }

        if (code == (int)ElevatedCloseReason.NotFound
            && await _elevated.WaitForExitAsync(target.Process, TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return TaskAnswer.Gone;
        }

        _log.Write(
            DiagnosticLevel.Warning,
            Area,
            "the scheduled task did not " + verb + " '" + name + "': " + ElevatedCloseReasons.Describe(code));
        return TaskAnswer.Refused;
    }

    /// <summary>The game the launcher in the foreground started: a descendant of it with a
    /// visible window, started during the stream and not protected. Exactly one, or nothing:
    /// with none or several the app does not guess.</summary>
    private (Target? Game, nint Window, CloseGameOutcome? Refused) FindLauncherGame(
        ProcessIdentity launcher, DateTimeOffset sessionStartedAt, AppSettings settings)
    {
        LaunchedWindows launched = _windows.GetWindowedDescendants(launcher);
        var games = new List<(Target Game, nint Window)>();
        foreach (var candidate in launched.Windows)
        {
            if (CloseGameProtection.Check(candidate.Process, sessionStartedAt, settings) == CloseGameProtectionReason.None)
            {
                games.Add((new Target(candidate.Process, false), candidate.Window));
            }
        }

        foreach (var candidate in launched.Elevated)
        {
            if (CloseGameProtection.Check(candidate.Process, sessionStartedAt, settings) == CloseGameProtectionReason.None)
            {
                games.Add((new Target(candidate.Process, true), candidate.Window));
            }
        }

        if (games.Count == 1)
        {
            _log.Write(
                DiagnosticLevel.Info,
                Area,
                "'" + launcher.Name + "' (the launcher) is in front; closing the game it started, '" + games[0].Game.Process.Name + "'");
            return (games[0].Game, games[0].Window, null);
        }

        if (games.Count > 1)
        {
            string count = games.Count.ToString(CultureInfo.InvariantCulture);
            string names = string.Join(", ", games.Select(game => game.Game.Process.Name));
            _log.Write(
                DiagnosticLevel.Info,
                Area,
                "'" + launcher.Name + "' (the launcher) is in front and started " + count + " programs with a window during the stream (" + names + "); the app does not guess, nothing was closed");
            return (null, 0, new CloseGameOutcome(CloseGameResult.AmbiguousTarget, launcher.Name, CloseGameProtectionReason.Launcher));
        }

        if (launched.NotInspectable > 0)
        {
            _log.Write(
                DiagnosticLevel.Warning,
                Area,
                "'" + launcher.Name + "' (the launcher) is in front and the game it started could not be identified; nothing was closed");
            return (null, 0, new CloseGameOutcome(CloseGameResult.NotInspectable, launcher.Name, CloseGameProtectionReason.Launcher));
        }

        _log.Write(
            DiagnosticLevel.Info,
            Area,
            "'" + launcher.Name + "' is protected (it is the launcher) and started no game with a window during the stream; nothing was closed");
        return (null, 0, new CloseGameOutcome(CloseGameResult.Protected, launcher.Name, CloseGameProtectionReason.Launcher));
    }

    private async Task<CloseGameOutcome> ForceAsync(Target target, AppSettings settings, CancellationToken cancellationToken)
    {
        if (target.Elevated)
        {
            return await ForceElevatedAsync(target, settings, cancellationToken).ConfigureAwait(false);
        }

        ProcessIdentity process = target.Process;
        bool protectedChild = false;
        foreach (var child in _windows.GetDescendants(process))
        {
            if (CloseGameProtection.CheckName(child.Name, settings) != CloseGameProtectionReason.None)
            {
                protectedChild = true;
                _log.Write(DiagnosticLevel.Warning, Area, "'" + process.Name + "' started the protected program '" + child.Name + "'; ending only the game itself");
                break;
            }
        }

        bool gone = protectedChild
            ? await _windows.KillProcessAsync(process, cancellationToken).ConfigureAwait(false)
            : await _processes.KillTreeAsync(process, cancellationToken).ConfigureAwait(false);

        if (!gone)
        {
            StartCooldown();
            _log.Write(
                DiagnosticLevel.Warning,
                Area,
                "could not end '" + process.Name + "'; it may run as administrator or be protected by anti-cheat");
            return new CloseGameOutcome(CloseGameResult.ForceFailed, process.Name);
        }

        return Finish(CloseGameResult.Forced, process, settings, "ended '" + process.Name + "'");
    }

    /// <summary>Ends a game that runs as administrator through the task: the game alone, since
    /// the task acts on one process with a window and the app cannot open the programs it
    /// started.</summary>
    private async Task<CloseGameOutcome> ForceElevatedAsync(Target target, AppSettings settings, CancellationToken cancellationToken)
    {
        ProcessIdentity process = target.Process;
        ElevatedCloseAttempt attempt = await _elevated.RunAsync(ElevatedCloseOperation.Kill, process, cancellationToken).ConfigureAwait(false);
        TaskAnswer answer = await ReadAnswerAsync(attempt, target, "end", cancellationToken).ConfigureAwait(false);
        if (answer == TaskAnswer.Gone)
        {
            return Finish(CloseGameResult.Closed, process, settings, "'" + process.Name + "' was already gone");
        }

        bool gone = answer == TaskAnswer.Proceed
            && await _elevated.WaitForExitAsync(process, _elevatedKillCheck, cancellationToken).ConfigureAwait(false);
        if (!gone)
        {
            StartCooldown();
            if (answer == TaskAnswer.Proceed)
            {
                _log.Write(
                    DiagnosticLevel.Warning,
                    Area,
                    "could not end '" + process.Name + "' (runs as administrator); it may be protected by anti-cheat");
            }

            return new CloseGameOutcome(CloseGameResult.ForceFailed, process.Name);
        }

        return Finish(CloseGameResult.Forced, process, settings, "ended '" + process.Name + "' (runs as administrator) through the scheduled task");
    }

    private CloseGameOutcome Finish(CloseGameResult result, ProcessIdentity target, AppSettings settings, string message)
    {
        StartCooldown();
        _log.Write(DiagnosticLevel.Info, Area, message);
        ReturnToLauncher(settings);
        return new CloseGameOutcome(result, target.Name);
    }

    private void StartCooldown()
    {
        lock (_gate)
        {
            _cooldownUntil = _time.GetUtcNow() + Cooldown;
        }
    }

    private void ReturnToLauncher(AppSettings settings)
    {
        if (!settings.Controller.CloseGame.ReturnToLauncher || settings.Launcher.ProcessNames.Count == 0)
        {
            return;
        }

        IReadOnlyList<ProcessIdentity> launchers = _processes.FindByNames(settings.Launcher.ProcessNames);
        if (launchers.Count == 0)
        {
            return;
        }

        bool front = _windows.BringToFront(launchers[0]);
        _log.Write(
            DiagnosticLevel.Info,
            Area,
            front
                ? "brought '" + launchers[0].Name + "' to the front"
                : "Windows did not let the app bring '" + launchers[0].Name + "' to the front");
    }

    private static string Describe(CloseGameProtectionReason reason) => reason switch
    {
        CloseGameProtectionReason.Fixed => "Windows, Sunshine or this app",
        CloseGameProtectionReason.Launcher => "it is the launcher",
        CloseGameProtectionReason.UserList => "on the protected list",
        CloseGameProtectionReason.StartedBeforeSession => "it was open before the stream began",
        _ => "protected",
    };

    /// <summary>The process a round acts on. <see cref="Elevated"/>: Windows does not let the app
    /// act on it, so it goes through the scheduled task.</summary>
    private sealed record Target(ProcessIdentity Process, bool Elevated);

    /// <summary>What a round does after the scheduled task answered.</summary>
    private enum TaskAnswer
    {
        /// <summary>The task acted (or its answer is unknown): check the process.</summary>
        Proceed,

        /// <summary>The process was already gone.</summary>
        Gone,

        /// <summary>The task did not act; the reason is in the log.</summary>
        Refused,
    }
}
