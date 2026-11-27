using System.Threading;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Application.Tests.CloseGame;

/// <summary>Pretend windows and processes. Each game can close when asked, ignore the
/// request, or survive being ended; the foreground window is whatever the test says.</summary>
internal sealed class FakeGameEnvironment : IGameWindowController, IProcessController
{
    public List<ProcessIdentity> Running { get; } = [];

    public ForegroundWindow Foreground { get; set; } = ForegroundWindow.Nothing;

    /// <summary>PIDs that exit when their window is asked to close.</summary>
    public HashSet<int> ClosesPolitely { get; } = [];

    /// <summary>PIDs that survive being ended.</summary>
    public HashSet<int> Unkillable { get; } = [];

    /// <summary>PIDs whose window refuses the close request (runs as administrator).</summary>
    public HashSet<int> DeniesClose { get; } = [];

    public Dictionary<int, List<ProcessIdentity>> Children { get; } = new();

    /// <summary>Descendants with a visible window, by the PID of the process that started them.</summary>
    public Dictionary<int, List<WindowedProcess>> WindowedChildren { get; } = new();

    /// <summary>Descendants with a window that cannot be inspected, by parent PID.</summary>
    public Dictionary<int, int> HiddenChildren { get; } = new();

    public bool BringToFrontWorks { get; set; } = true;

    /// <summary>When set, <see cref="WaitForExitAsync"/> waits for it before answering.</summary>
    public TaskCompletionSource? WaitGate { get; set; }

    public List<string> Calls { get; } = [];

    public ProcessIdentity Start(int id, string name, DateTimeOffset startedAt)
    {
        var process = new ProcessIdentity(id, name, startedAt);
        Running.Add(process);
        return process;
    }

    public void Focus(ProcessIdentity process) => Foreground = new ForegroundWindow(ForegroundStatus.Found, process, process.Id * 10);

    public ForegroundWindow GetForeground()
    {
        Calls.Add("foreground");
        return Foreground;
    }

    public WindowCloseResult RequestCloseWindow(ProcessIdentity process, nint window)
    {
        Calls.Add($"closeWindow:{process.Id}:{window}");
        if (!Running.Contains(process))
        {
            return WindowCloseResult.Gone;
        }

        if (DeniesClose.Contains(process.Id))
        {
            return WindowCloseResult.Denied;
        }

        if (ClosesPolitely.Contains(process.Id))
        {
            Running.Remove(process);
        }

        return WindowCloseResult.Sent;
    }

    public IReadOnlyList<ProcessIdentity> GetDescendants(ProcessIdentity process) =>
        Children.TryGetValue(process.Id, out var children) ? children : [];

    public LaunchedWindows GetWindowedDescendants(ProcessIdentity process)
    {
        Calls.Add($"windowedChildren:{process.Id}");
        List<WindowedProcess> windows = WindowedChildren.TryGetValue(process.Id, out var found)
            ? found.Where(child => Running.Contains(child.Process)).ToList()
            : [];
        return new LaunchedWindows(windows, HiddenChildren.GetValueOrDefault(process.Id));
    }

    /// <summary>Starts <paramref name="name"/> as a windowed child of <paramref name="parent"/>.</summary>
    public ProcessIdentity StartWindowedChild(ProcessIdentity parent, int id, string name, DateTimeOffset startedAt)
    {
        var child = Start(id, name, startedAt);
        if (!WindowedChildren.TryGetValue(parent.Id, out var list))
        {
            list = [];
            WindowedChildren[parent.Id] = list;
        }

        list.Add(new WindowedProcess(child, id * 10));
        return child;
    }

    public Task<bool> KillProcessAsync(ProcessIdentity process, CancellationToken cancellationToken)
    {
        Calls.Add($"killOne:{process.Id}");
        return Task.FromResult(End(process));
    }

    public bool BringToFront(ProcessIdentity process)
    {
        Calls.Add($"front:{process.Name}");
        return BringToFrontWorks;
    }

    public IReadOnlyList<ProcessIdentity> FindByNames(IReadOnlyCollection<string> names) =>
        [.. Running.Where(process => names.Contains(process.Name, StringComparer.OrdinalIgnoreCase))];

    public bool RequestClose(ProcessIdentity process) => throw new NotSupportedException("o fechar o jogo usa RequestCloseWindow");

    public async Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Calls.Add($"wait:{process.Id}:{(int)timeout.TotalMilliseconds}");
        if (WaitGate is not null)
        {
            await WaitGate.Task.WaitAsync(cancellationToken);
        }

        return !Running.Contains(process);
    }

    public Task<bool> KillTreeAsync(ProcessIdentity process, CancellationToken cancellationToken)
    {
        Calls.Add($"killTree:{process.Id}");
        return Task.FromResult(End(process));
    }

    private bool End(ProcessIdentity process)
    {
        if (!Unkillable.Contains(process.Id))
        {
            Running.Remove(process);
        }

        return !Running.Contains(process);
    }
}
