using Shouldly;
using SunshineHelper.Application.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>The two restore behaviours the launcher module needs (docs/adr/0008-modulo-launcher.md):
/// leaving a module alone on a plain disconnect, and a revert that succeeds with a warning.</summary>
public class SessionOrchestratorLeaveAndWarnTests
{
    [Fact]
    public async Task RestoreAsync_deixa_de_fora_o_modulo_pedido_e_apaga_o_snapshot()
    {
        var test = new OrchestratorTest();
        await ApplyAsync(test);

        var result = await test.Orchestrator.RestoreAsync(
            new HashSet<string>(StringComparer.Ordinal) { "c" },
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.StepsOf("revert").ShouldBe("b:revert,a:revert");
        test.Environment.ShouldBe("original,original,applied");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_com_aviso_de_um_modulo_conclui_incompleto_e_apaga_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Modules[2].RevertWarns = "Não foi possível encerrar: Launcher (PID 7).";
        await ApplyAsync(test);

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Incomplete);
        result.Problems.ShouldHaveSingleItem().ShouldContain("PID 7");
        test.Environment.ShouldBe("original,original,original");
        test.Store.Content.ShouldBeNull();
    }

    private static Task<SessionApplyResult> ApplyAsync(OrchestratorTest test) =>
        test.Orchestrator.ApplyAsync(
            TestProfiles.FollowClient(),
            TestProfiles.Request(),
            TestContext.Current.CancellationToken);
}
