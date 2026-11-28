using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Infrastructure.Launcher;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace SunshineHelper.Infrastructure.CloseGame;

/// <summary>
/// <see cref="IGameWindowController"/> over the Win32 window functions. Like
/// <see cref="WindowsProcessController"/>, every action first confirms that the process is
/// still the one that was identified (same PID and start time) and does nothing otherwise.
/// </summary>
/// <remarks>
/// <para>This app is not elevated. A program running as administrator cannot be opened (.NET
/// cannot read its start time) and does not take messages from a program that is not elevated.
/// Such a program is identified from the system's process list instead
/// (<see cref="SystemProcessList"/>) and reported as <see cref="ForegroundStatus.Elevated"/> or in
/// <see cref="LaunchedWindows.Elevated"/>; the service then goes through the scheduled task
/// (docs/adr/0017-fechar-jogo-de-administrador.md).</para>
/// </remarks>
public sealed class WindowsGameWindowController : IGameWindowController
{
    private const uint WmClose = 0x0010;
    private const int ErrorAccessDenied = 5;

    private static readonly TimeSpan _killSettleTime = TimeSpan.FromSeconds(5);

    /// <summary>Slack when comparing a child's start time with its parent's.</summary>
    private static readonly TimeSpan _parentTolerance = TimeSpan.FromSeconds(1);

    /// <summary>The id of the process that owns <paramref name="window"/>, or 0 when the
    /// window no longer exists.</summary>
    private static unsafe uint OwnerProcessId(HWND window)
    {
        uint processId = 0;
        uint thread = PInvoke.GetWindowThreadProcessId(window, &processId);
        return thread == 0 ? 0 : processId;
    }

    public ForegroundWindow GetForeground()
    {
        HWND window = PInvoke.GetForegroundWindow();
        if (window.Value == 0)
        {
            return ForegroundWindow.Nothing;
        }

        uint processId = OwnerProcessId(window);
        if (processId == 0)
        {
            return ForegroundWindow.Nothing;
        }

        Process process;
        try
        {
            process = Process.GetProcessById((int)processId);
        }
        catch (ArgumentException)
        {
            return ForegroundWindow.Nothing;
        }

        using (process)
        {
            if (WindowsProcessController.TryIdentify(process) is { } identity)
            {
                return new ForegroundWindow(ForegroundStatus.Found, identity, window.Value);
            }
        }

        // Most likely a program that runs as administrator: identified without opening it.
        return SystemProcessList.Find((int)processId) is { } listed && OwnerProcessId(window) == processId
            ? new ForegroundWindow(ForegroundStatus.Elevated, listed.ToIdentity(), window.Value)
            : new ForegroundWindow(ForegroundStatus.NotInspectable);
    }

    public WindowCloseResult RequestCloseWindow(ProcessIdentity process, nint window)
    {
        ArgumentNullException.ThrowIfNull(process);

        using Process? live = WindowsProcessController.FindLive(process);
        if (live is null)
        {
            return WindowCloseResult.Gone;
        }

        var handle = new HWND(window);
        if (OwnerProcessId(handle) != (uint)process.Id)
        {
            // The window is gone, or its handle now belongs to another process.
            return WindowCloseResult.Gone;
        }

        bool posted = PInvoke.PostMessage(handle, WmClose, default, default);
        if (posted)
        {
            return WindowCloseResult.Sent;
        }

        return Marshal.GetLastWin32Error() == ErrorAccessDenied
            ? WindowCloseResult.Denied
            : WindowCloseResult.Gone;
    }

    public IReadOnlyList<ProcessIdentity> GetDescendants(ProcessIdentity process)
    {
        ArgumentNullException.ThrowIfNull(process);

        using (Process? live = WindowsProcessController.FindLive(process))
        {
            if (live is null)
            {
                return [];
            }
        }

        var found = new List<ProcessIdentity>();
        foreach (int id in ProcessTree.DescendantsOf(process.Id))
        {
            try
            {
                using Process child = Process.GetProcessById(id);
                // A child cannot be older than its parent; an older one is a recycled PID.
                if (WindowsProcessController.TryIdentify(child) is { } identity
                    && identity.StartedAt >= process.StartedAt - _parentTolerance)
                {
                    found.Add(identity);
                }
            }
            catch (ArgumentException)
            {
                // Ended since the snapshot.
            }
        }

        return found;
    }

    public LaunchedWindows GetWindowedDescendants(ProcessIdentity process)
    {
        ArgumentNullException.ThrowIfNull(process);

        // The launcher itself may run as administrator, so it is looked up in the system's list
        // when it cannot be opened.
        IReadOnlyList<SystemProcess> system = SystemProcessList.Read();
        using (Process? live = WindowsProcessController.FindLive(process))
        {
            if (live is null && !SystemProcessList.Contains(system, process))
            {
                return LaunchedWindows.None;
            }
        }

        var windows = new List<WindowedProcess>();
        var elevated = new List<WindowedProcess>();
        int notInspectable = 0;
        foreach (int id in ProcessTree.DescendantsOf(process.Id))
        {
            try
            {
                using Process child = Process.GetProcessById(id);

                // Found by process id alone, so it works even for a program running as
                // administrator; zero means no visible top-level window.
                nint window = child.MainWindowHandle;
                if (window == 0)
                {
                    continue;
                }

                ProcessIdentity? identity = WindowsProcessController.TryIdentify(child);
                bool admin = identity is null;
                if (admin)
                {
                    identity = SystemProcessList.Find(system, id) is { } listed ? listed.ToIdentity() : null;
                }

                if (identity is null)
                {
                    notInspectable++;
                    continue;
                }

                // A child cannot be older than its parent; an older one is a recycled PID.
                if (identity.StartedAt >= process.StartedAt - _parentTolerance)
                {
                    (admin ? elevated : windows).Add(new WindowedProcess(identity, window));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Ended since the snapshot, or out of reach.
            }
        }

        return new LaunchedWindows(windows, notInspectable, elevated);
    }

    public async Task<bool> KillProcessAsync(ProcessIdentity process, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        using (Process? live = WindowsProcessController.FindLive(process))
        {
            if (live is null)
            {
                return true;
            }

            try
            {
                live.Kill(entireProcessTree: false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Already gone, or refused; whether it is still there is checked below.
            }

            using var settle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            settle.CancelAfter(_killSettleTime);
            try
            {
                await live.WaitForExitAsync(settle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Did not go away in time; reported by the lookup below.
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Same.
            }
        }

        using Process? stillThere = WindowsProcessController.FindLive(process);
        return stillThere is null;
    }

    public bool BringToFront(ProcessIdentity process)
    {
        ArgumentNullException.ThrowIfNull(process);

        using Process? live = WindowsProcessController.FindLive(process);
        if (live is null)
        {
            return false;
        }

        try
        {
            nint window = live.MainWindowHandle;
            return window != 0 && PInvoke.SetForegroundWindow(new HWND(window));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
