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
/// </remarks>
public sealed class CloseGameService
{
    /// <summary>How long a second hold still confirms ending the game that did not close.</summary>
    public static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(15);

    /// <summary>How long the combo is ignored after a round ends.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private const string Area = "closeGame";

    private readonly IGameWindowController _windows;
    private readonly IProcessController _processes;
    private readonly ICurrentSettingsProvider _settings;
    private readonly SessionStatusReader _session;
    private readonly TimeProvider _time;
    private readonly IDiagnosticLog _log;
    private readonly Lock _gate = new();

    private int _busy;
    private ProcessIdentity? _pending;
    private DateTimeOffset _pendingUntil;
    private DateTimeOffset _cooldownUntil;

    public CloseGameService(
        IGameWindowController windows,
        IProcessController processes,
        ICurrentSettingsProvider settings,
        SessionStatusReader session,
        TimeProvider? timeProvider = null,
        IDiagnosticLog? log = null)
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
    }

    /// <summary>The game waiting for a second hold, if any (for the overlay to show
    /// "hold again to end it" later).</summary>
    public ProcessIdentity? PendingConfirmation
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null && _time.GetUtcNow() < _pendingUntil ? _pending : null;
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
        ProcessIdentity? pending;
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
                _log.Write(DiagnosticLevel.Info, Area, "the confirmation to end '" + pending.Name + "' expired; starting over");
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
                "the program in the foreground cannot be inspected; it probably runs as administrator, so the app cannot close it (in Sunshine, untick 'Run as administrator' for the launcher app)");
            return new CloseGameOutcome(CloseGameResult.NotInspectable);
        }

        if (foreground.Status != ForegroundStatus.Found || foreground.Process is not { } target)
        {
            _log.Write(DiagnosticLevel.Info, Area, "combo ignored: no window in the foreground");
            return new CloseGameOutcome(CloseGameResult.NoTarget);
        }

        CloseGameProtectionReason reason = CloseGameProtection.Check(target, sessionStartedAt, settings);
        if (reason != CloseGameProtectionReason.None)
        {
            _log.Write(DiagnosticLevel.Info, Area, "'" + target.Name + "' is protected (" + Describe(reason) + "); nothing was closed");
            return new CloseGameOutcome(CloseGameResult.Protected, target.Name, reason);
        }

        WindowCloseResult request = _windows.RequestCloseWindow(target, foreground.Window);
        if (request == WindowCloseResult.Denied)
        {
            _log.Write(
                DiagnosticLevel.Warning,
                Area,
                "Windows refused to ask '" + target.Name + "' to close; it probably runs as administrator (in Sunshine, untick 'Run as administrator' for the launcher app)");
            return new CloseGameOutcome(CloseGameResult.AccessDenied, target.Name);
        }

        if (request == WindowCloseResult.Gone)
        {
            return Finish(CloseGameResult.Closed, target, settings, "'" + target.Name + "' was already gone");
        }

        TimeSpan timeout = TimeSpan.FromMilliseconds(closeGame.PoliteTimeoutMs);
        string timeoutText = closeGame.PoliteTimeoutMs.ToString(CultureInfo.InvariantCulture);
        _log.Write(DiagnosticLevel.Info, Area, "asked '" + target.Name + "' to close; waiting up to " + timeoutText + " ms");

        bool exited = await _processes.WaitForExitAsync(target, timeout, cancellationToken).ConfigureAwait(false);
        if (exited)
        {
            return Finish(CloseGameResult.Closed, target, settings, "'" + target.Name + "' closed");
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
            "'" + target.Name + "' did not close; hold the combo again within " + windowText + " s to end it");
        return new CloseGameOutcome(CloseGameResult.AwaitingConfirmation, target.Name);
    }

    private async Task<CloseGameOutcome> ForceAsync(ProcessIdentity target, AppSettings settings, CancellationToken cancellationToken)
    {
        bool protectedChild = false;
        foreach (var child in _windows.GetDescendants(target))
        {
            if (CloseGameProtection.CheckName(child.Name, settings) != CloseGameProtectionReason.None)
            {
                protectedChild = true;
                _log.Write(DiagnosticLevel.Warning, Area, "'" + target.Name + "' started the protected program '" + child.Name + "'; ending only the game itself");
                break;
            }
        }

        bool gone = protectedChild
            ? await _windows.KillProcessAsync(target, cancellationToken).ConfigureAwait(false)
            : await _processes.KillTreeAsync(target, cancellationToken).ConfigureAwait(false);

        if (!gone)
        {
            StartCooldown();
            _log.Write(
                DiagnosticLevel.Warning,
                Area,
                "could not end '" + target.Name + "'; it may run as administrator or be protected by anti-cheat");
            return new CloseGameOutcome(CloseGameResult.ForceFailed, target.Name);
        }

        return Finish(CloseGameResult.Forced, target, settings, "ended '" + target.Name + "'");
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
}
