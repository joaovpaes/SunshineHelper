using System.Threading;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Application.Tests.Launcher;

/// <summary>A pretend process list. Each process can be told to close when asked, to
/// ignore the request, or to survive even being killed.</summary>
internal sealed class FakeProcessController : IProcessController
{
    public List<ProcessIdentity> Running { get; } = [];

    /// <summary>Processes (by PID) that exit when their window is closed.</summary>
    public HashSet<int> ClosesPolitely { get; } = [];

    /// <summary>Processes (by PID) that survive even being killed.</summary>
    public HashSet<int> Unkillable { get; } = [];

    public List<string> Calls { get; } = [];

    public ProcessIdentity Start(int id, string name, DateTimeOffset startedAt)
    {
        var process = new ProcessIdentity(id, name, startedAt);
        Running.Add(process);
        return process;
    }

    public IReadOnlyList<ProcessIdentity> FindByNames(IReadOnlyCollection<string> names)
    {
        Calls.Add($"find:{string.Join('|', names)}");
        return [.. Running.Where(process => names.Contains(process.Name, StringComparer.OrdinalIgnoreCase))];
    }

    public bool RequestClose(ProcessIdentity process)
    {
        Calls.Add($"close:{process.Id}");
        if (ClosesPolitely.Contains(process.Id))
        {
            Running.Remove(process);
        }

        return true;
    }

    public Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Calls.Add($"wait:{process.Id}:{(int)timeout.TotalMilliseconds}");
        return Task.FromResult(!Running.Contains(process));
    }

    public Task<bool> KillTreeAsync(ProcessIdentity process, CancellationToken cancellationToken)
    {
        Calls.Add($"kill:{process.Id}");
        if (!Unkillable.Contains(process.Id))
        {
            Running.Remove(process);
        }

        return Task.FromResult(!Running.Contains(process));
    }
}
