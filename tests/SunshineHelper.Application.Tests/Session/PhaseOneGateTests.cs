using Shouldly;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>
/// The Phase 1 gate (docs/fase1-plano.md, "O que o portão da Fase 1 exige") played out the
/// way it happens for real: a first copy of the app runs a session and stops dead at some
/// point — no rollback, no cleanup, only whatever it had already written to the snapshot
/// file — and a second copy of the app, started afterwards over the same environment,
/// puts everything back from that file. Unlike the restore tests, no snapshot here is
/// written by hand: each one is exactly what the orchestrator itself left on disk.
/// </summary>
public class PhaseOneGateTests
{
    private const string AllOriginal = "original,original,original";

    private static readonly TimeSpan _reachTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Gate item 2: app killed while applying module 0, 1 or 2 (a, b, c).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task App_encerrado_no_meio_da_aplicacao_e_restaurado_ao_abrir_de_novo(int dyingModule)
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var dying = test.Modules[dyingModule];
        var reached = new TaskCompletionSource();
        dying.WhileApplying = () => reached.TrySetResult();
        dying.ApplyHangs = true;

        var firstRun = test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);
        await reached.Task.WaitAsync(_reachTimeout, ct);

        firstRun.IsCompleted.ShouldBeFalse();
        string leftOnDisk = test.Store.Content.ShouldNotBeNull();
        SessionSnapshotSerializer.Deserialize(leftOnDisk).State.ShouldBe(SessionState.Applying);

        dying.ApplyHangs = false;
        var secondRun = SecondCopyOfTheApp(test, leftOnDisk, out var store);
        var result = await secondRun.RestoreAsync(ct);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.Environment.ShouldBe(AllOriginal);
        store.Content.ShouldBeNull();
    }

    /// <summary>Gate item 2: app killed after everything was applied, during the stream.</summary>
    [Fact]
    public async Task App_encerrado_durante_a_transmissao_e_restaurado_ao_abrir_de_novo()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();

        var applied = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);
        applied.Status.ShouldBe(SessionApplyStatus.Applied);
        test.Environment.ShouldBe("applied,applied,applied");

        var secondRun = SecondCopyOfTheApp(test, test.Store.Content.ShouldNotBeNull(), out var store);
        var result = await secondRun.RestoreAsync(ct);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.Environment.ShouldBe(AllOriginal);
        test.StepsOf("revert").ShouldBe("c:revert,b:revert,a:revert");
        store.Content.ShouldBeNull();
    }

    /// <summary>The app dies a second time, now while putting things back (revert runs c,
    /// b, a, so dying at b means c was already back). The third copy finishes the job.</summary>
    [Fact]
    public async Task App_encerrado_no_meio_da_restauracao_termina_de_restaurar_na_proxima_vez()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);

        var dying = test.Modules[1];
        var reached = new TaskCompletionSource();
        dying.WhileReverting = () => reached.TrySetResult();
        dying.RevertHangs = true;

        var interrupted = test.Orchestrator.RestoreAsync(ct);
        await reached.Task.WaitAsync(_reachTimeout, ct);

        interrupted.IsCompleted.ShouldBeFalse();
        test.Environment.ShouldBe("applied,applied,original");
        string leftOnDisk = test.Store.Content.ShouldNotBeNull();
        SessionSnapshotSerializer.Deserialize(leftOnDisk).State.ShouldBe(SessionState.Reverting);

        dying.RevertHangs = false;
        dying.WhileReverting = null;
        var thirdRun = SecondCopyOfTheApp(test, leftOnDisk, out var store);
        var result = await thirdRun.RestoreAsync(ct);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.Environment.ShouldBe(AllOriginal);
        store.Content.ShouldBeNull();
    }

    /// <summary>Gate item 3: the PC restarts mid-session. Only the module whose change
    /// outlives a restart (the RTSS profile, here c) is put back; the ones Windows resets by
    /// itself (video, HDR) are not touched.</summary>
    [Fact]
    public async Task Reinicio_no_meio_da_sessao_desfaz_so_o_que_sobrevive_ao_reinicio()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        test.Modules[2].SurvivesReboot = true;
        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);
        string leftOnDisk = test.Store.Content.ShouldNotBeNull();

        test.Boot.Current = new BootIdentity { Id = "boot-2", UptimeMs = 60_000 };
        var afterReboot = SecondCopyOfTheApp(test, leftOnDisk, out var store);
        var result = await afterReboot.RestoreAsync(ct);

        result.Status.ShouldBe(RestoreStatus.Restored);
        result.RebootDetected.ShouldBeTrue();
        test.StepsOf("revert").ShouldBe("c:revert");
        test.Modules[2].Value.ShouldBe(FakeEnvironmentModule.Original);
        store.Content.ShouldBeNull();
    }

    /// <summary>Gate item 1, end of session: a module fails to put its part back. The
    /// others are still restored, the snapshot stays, and once the cause is gone the next
    /// start of the app finishes the job and new sessions can begin again.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Falha_ao_restaurar_um_modulo_e_resolvida_na_proxima_abertura(int failingModule)
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);

        var failing = test.Modules[failingModule];
        failing.RevertFails = "O RTSS não estava rodando.";
        var first = await test.Orchestrator.RestoreAsync(ct);

        first.Status.ShouldBe(RestoreStatus.Failed);
        failing.Value.ShouldBe(FakeEnvironmentModule.Applied);
        test.Modules.Where(m => m != failing).ShouldAllBe(m => m.Value == FakeEnvironmentModule.Original);
        string leftOnDisk = test.Store.Content.ShouldNotBeNull();

        // A new session is refused while the old one is not fully undone.
        var refused = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);
        refused.Status.ShouldBe(SessionApplyStatus.Refused);

        failing.RevertFails = null;
        var nextStart = SecondCopyOfTheApp(test, leftOnDisk, out var store);
        var second = await nextStart.RestoreAsync(ct);

        second.Status.ShouldBe(RestoreStatus.Restored);
        test.Environment.ShouldBe(AllOriginal);
        store.Content.ShouldBeNull();

        var again = await nextStart.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), ct);
        again.Status.ShouldBe(SessionApplyStatus.Applied);
    }

    /// <summary>A fresh orchestrator over the same environment (the modules keep their
    /// values, as the PC would) and a store holding only what was on disk.</summary>
    private static SessionOrchestrator SecondCopyOfTheApp(OrchestratorTest test, string snapshotOnDisk, out FakeSessionSnapshotStore store)
    {
        store = new FakeSessionSnapshotStore { Content = snapshotOnDisk };
        return new SessionOrchestrator(test.Modules, store, test.Boot, TimeProvider.System);
    }
}
