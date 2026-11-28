using SunshineHelper.Trigger.ElevatedClose;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

/// <summary>A made-up process list for the rules: the target, its ancestors, and how opening goes.</summary>
internal sealed class FakeProcessSystem : IElevatedProcessSystem
{
    public Dictionary<int, ProcessNode> Nodes { get; } = [];

    public FakeTarget? Target { get; set; }

    public OpenStatus OpenResult { get; set; } = OpenStatus.Opened;

    public bool? OpenedForKill { get; private set; }

    public OpenStatus Open(int processId, bool forKill, out ITargetProcess? process)
    {
        OpenedForKill = forKill;
        process = null;
        if (OpenResult != OpenStatus.Opened)
        {
            return OpenResult;
        }

        if (Target is null || Target.Node.ProcessId != processId)
        {
            return OpenStatus.NotFound;
        }

        process = Target;
        return OpenStatus.Opened;
    }

    public ProcessNode? Describe(int processId) => Nodes.TryGetValue(processId, out ProcessNode? node) ? node : null;

    public void Add(ProcessNode node) => Nodes[node.ProcessId] = node;
}

/// <summary>A target that passes every rule unless a test says otherwise, and records what was done.</summary>
internal sealed class FakeTarget(ProcessNode node) : ITargetProcess
{
    public ProcessNode Node { get; } = node;

    public bool IsElevated { get; set; } = true;

    public bool BelongsToCaller { get; set; } = true;

    public bool IsCritical { get; set; }

    public bool HasVisibleWindow { get; set; } = true;

    public bool ActionSucceeds { get; set; } = true;

    public List<string> Actions { get; } = [];

    public bool Disposed { get; private set; }

    public bool RequestClose()
    {
        Actions.Add("close");
        return ActionSucceeds;
    }

    public bool Kill()
    {
        Actions.Add("kill");
        return ActionSucceeds;
    }

    public void Dispose() => Disposed = true;
}
