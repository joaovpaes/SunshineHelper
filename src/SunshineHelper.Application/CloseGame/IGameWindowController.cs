using System.Threading;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Application.CloseGame;

/// <summary>What the foreground window lookup found.</summary>
public enum ForegroundStatus
{
    /// <summary>No window is in the foreground (the desktop, a locked screen).</summary>
    None,

    /// <summary>A window and the process that owns it.</summary>
    Found,

    /// <summary>A window whose process Windows does not let this app inspect: usually a
    /// program running as administrator while the app is not.</summary>
    NotInspectable,
}

/// <summary>The window in the foreground and its process, when the process could be
/// identified (docs/adr/0016-fechar-jogo-pelo-controle.md, "Quem é o alvo").</summary>
public sealed record ForegroundWindow(ForegroundStatus Status, ProcessIdentity? Process = null, nint Window = 0)
{
    public static ForegroundWindow Nothing { get; } = new(ForegroundStatus.None);
}

/// <summary>A process and its visible main window.</summary>
public sealed record WindowedProcess(ProcessIdentity Process, nint Window);

/// <summary>The programs a launcher started that show a window, for the case where the
/// launcher keeps the foreground while its game is on screen
/// (docs/adr/0016-fechar-jogo-pelo-controle.md, "Quem é o alvo").</summary>
/// <param name="Windows">Descendants of the launcher with a visible main window.</param>
/// <param name="NotInspectable">Descendants with a window that Windows did not let the app
/// inspect, usually because they run as administrator.</param>
public sealed record LaunchedWindows(IReadOnlyList<WindowedProcess> Windows, int NotInspectable)
{
    public static LaunchedWindows None { get; } = new([], 0);
}

/// <summary>How a polite "close your window" request went.</summary>
public enum WindowCloseResult
{
    /// <summary>The request reached the window; the game decides what to do with it.</summary>
    Sent,

    /// <summary>The window or the process is already gone (or the PID now belongs to
    /// something else).</summary>
    Gone,

    /// <summary>Windows refused to deliver it: a window of a program running as
    /// administrator does not take messages from a program that is not.</summary>
    Denied,
}

/// <summary>
/// The window-level operations the close-game action needs, kept behind an interface so
/// every rule that decides <em>what</em> to close is tested without Windows. Process-level
/// operations (waiting, ending a tree, finding the launcher) come from
/// <see cref="IProcessController"/>, shared with the launcher module (ADR 0008).
/// </summary>
/// <remarks>Like <see cref="IProcessController"/>, every method that acts on a process
/// first confirms that the <see cref="ProcessIdentity"/> still names the same process (same
/// PID and start time) and does nothing otherwise.</remarks>
public interface IGameWindowController
{
    /// <summary>The window in the foreground right now and its process.</summary>
    ForegroundWindow GetForeground();

    /// <summary>Asks <paramref name="window"/> to close, like clicking its X
    /// (<c>WM_CLOSE</c>).</summary>
    WindowCloseResult RequestCloseWindow(ProcessIdentity process, nint window);

    /// <summary>Every process started by <paramref name="process"/>, children and their
    /// children. Processes that cannot be inspected are left out.</summary>
    IReadOnlyList<ProcessIdentity> GetDescendants(ProcessIdentity process);

    /// <summary>Every process started by <paramref name="process"/> (children and their
    /// children) that shows a visible main window, with that window.</summary>
    LaunchedWindows GetWindowedDescendants(ProcessIdentity process);

    /// <summary>Ends <paramref name="process"/> alone, leaving its children. True when it is
    /// gone afterwards (or was already gone).</summary>
    Task<bool> KillProcessAsync(ProcessIdentity process, CancellationToken cancellationToken);

    /// <summary>Tries to put the main window of <paramref name="process"/> in the foreground.
    /// Windows may refuse a background program this; false then.</summary>
    bool BringToFront(ProcessIdentity process);
}
