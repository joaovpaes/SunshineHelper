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
/// <para>This app is not elevated. A program running as administrator cannot be inspected
/// (its start time is refused) and does not take messages from a program that is not
/// elevated; both show up here as <see cref="ForegroundStatus.NotInspectable"/> and
/// <see cref="WindowCloseResult.Denied"/>, which the service turns into the log line that
/// tells the user to untick "Run as administrator" in Sunshine (ADR 0016, section 6).</para>
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
            ProcessIdentity? identity = WindowsProcessController.TryIdentify(process);
            return identity is null
                ? new ForegroundWindow(ForegroundStatus.NotInspectable)
                : new ForegroundWindow(ForegroundStatus.Found, identity, window.Value);
        }
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

        using (Process? live = WindowsProcessController.FindLive(process))
        {
            if (live is null)
            {
                return LaunchedWindows.None;
            }
        }

        var windows = new List<WindowedProcess>();
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

                if (WindowsProcessController.TryIdentify(child) is not { } identity)
                {
                    notInspectable++;
                    continue;
                }

                // A child cannot be older than its parent; an older one is a recycled PID.
                if (identity.StartedAt >= process.StartedAt - _parentTolerance)
                {
                    windows.Add(new WindowedProcess(identity, window));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Ended since the snapshot, or out of reach.
            }
        }

        return new LaunchedWindows(windows, notInspectable);
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
