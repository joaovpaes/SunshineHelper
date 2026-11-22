using SunshineHelper.Application.Session;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>A whole orchestrator wired to fakes: three modules named a, b and c, an
/// in-memory snapshot store and a boot identity that can be told what to say. Everything
/// they do lands in one shared log, in order, so a test can assert not just the result
/// but the sequence that produced it.</summary>
internal sealed class OrchestratorTest
{
    public OrchestratorTest(int[]? orders = null)
    {
        int[] moduleOrders = orders ?? [1, 2, 3];

        Log = [];
        Modules =
        [
            new FakeEnvironmentModule("a", moduleOrders[0], Log),
            new FakeEnvironmentModule("b", moduleOrders[1], Log),
            new FakeEnvironmentModule("c", moduleOrders[2], Log),
        ];
        Store = new FakeSessionSnapshotStore(Log);
        Boot = new FakeBootIdentityProvider();
        Orchestrator = new SessionOrchestrator(Modules, Store, Boot, TimeProvider.System);
    }

    public List<string> Log { get; }

    /// <summary>Always in the order a, b, c, whatever apply order they were given.</summary>
    public FakeEnvironmentModule[] Modules { get; }

    public FakeSessionSnapshotStore Store { get; }

    public FakeBootIdentityProvider Boot { get; }

    public SessionOrchestrator Orchestrator { get; }

    /// <summary>The whole make-believe environment, as a, b, c.</summary>
    public string Environment => string.Join(",", Modules.Select(module => module.Value));

    /// <summary>The steps of one kind, in the order they happened: "a:apply,b:apply".</summary>
    public string StepsOf(string step) =>
        string.Join(",", Log.Where(entry => entry.EndsWith($":{step}", StringComparison.Ordinal)));
}
