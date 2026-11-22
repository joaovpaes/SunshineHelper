using System.Text.Json.Nodes;
using Shouldly;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

public class SessionOrchestratorRestoreTests
{
    [Fact]
    public async Task RestoreAsync_sem_snapshot_nao_faz_nada()
    {
        var test = new OrchestratorTest();

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.NothingToDo);
        test.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_no_fim_da_sessao_devolve_tudo_na_ordem_inversa_e_apaga_o_snapshot()
    {
        var test = new OrchestratorTest();
        await test.Orchestrator.ApplyAsync(
            TestProfiles.FollowClient(),
            TestProfiles.Request(),
            TestContext.Current.CancellationToken);

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.StepsOf("revert").ShouldBe("c:revert,b:revert,a:revert");
        test.Environment.ShouldBe("original,original,original");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_apos_queda_no_meio_da_aplicacao_desfaz_o_modulo_interrompido()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson(
        [
            Captured("a", order: 1, ModuleApplyState.Applied),
            Captured("b", order: 2, ModuleApplyState.Applying),
            Captured("c", order: 3, ModuleApplyState.Pending),
        ],
        state: SessionState.Applying);

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.StepsOf("revert").ShouldBe("b:revert,a:revert");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_apos_reinicio_desfaz_so_o_que_sobrevive_ao_reinicio()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson(
        [
            Captured("a", order: 1, ModuleApplyState.Applied),
            Captured("b", order: 2, ModuleApplyState.Applied),
            Captured("c", order: 3, ModuleApplyState.Applied, survivesReboot: true),
        ]);

        // Ligado há menos tempo do que quando o snapshot foi gravado: o PC reiniciou.
        test.Boot.Current = new BootIdentity { Id = "boot-2", UptimeMs = 60_000 };

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Restored);
        result.RebootDetected.ShouldBeTrue();
        test.StepsOf("revert").ShouldBe("c:revert");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_sem_nada_para_desfazer_apaga_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson(
        [
            Captured("a", order: 1, ModuleApplyState.Skipped),
            Captured("b", order: 2, ModuleApplyState.Skipped),
        ]);

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Restored);
        test.StepsOf("revert").ShouldBeEmpty();
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_marca_a_sessao_como_revertendo_antes_de_desfazer()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson([Captured("a", order: 1, ModuleApplyState.Applied)]);
        string? onDiskDuringRevert = null;
        test.Modules[0].WhileReverting = () => onDiskDuringRevert = test.Store.Content;

        await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        onDiskDuringRevert.ShouldNotBeNull();
        SessionSnapshotSerializer.Deserialize(onDiskDuringRevert!).State.ShouldBe(SessionState.Reverting);
    }

    [Fact]
    public async Task RestoreAsync_modulo_que_falha_ao_desfazer_mantem_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson(
        [
            Captured("a", order: 1, ModuleApplyState.Applied),
            Captured("b", order: 2, ModuleApplyState.Applied),
        ]);
        test.Modules[1].RevertFails = "O RTSS não estava rodando.";

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Failed);
        result.Problems.ShouldContain(problem => problem.Contains("O RTSS não estava rodando.", StringComparison.Ordinal));
        test.Store.Content.ShouldNotBeNull();
        SessionSnapshotSerializer.Deserialize(test.Store.Content!).State.ShouldBe(SessionState.RevertFailed);

        // O que podia voltar, voltou: um módulo com problema não impede os outros.
        test.StepsOf("revert").ShouldBe("b:revert,a:revert");
    }

    [Fact]
    public async Task RestoreAsync_modulo_que_nao_existe_mais_nao_bloqueia_as_proximas_sessoes()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson(
        [
            Captured("a", order: 1, ModuleApplyState.Applied),
            Captured("fantasma", order: 2, ModuleApplyState.Applied),
        ]);

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Incomplete);
        result.Problems.ShouldContain(problem => problem.Contains("fantasma", StringComparison.Ordinal));
        test.StepsOf("revert").ShouldBe("a:revert");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_snapshot_ilegivel_mantem_o_arquivo()
    {
        var test = new OrchestratorTest();
        test.Store.Content = "{ isso não é json";

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Failed);
        test.Store.Content.ShouldNotBeNull();
        test.StepsOf("revert").ShouldBeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_erro_de_leitura_nao_derruba_o_app()
    {
        var test = new OrchestratorTest();
        test.Store.ThrowOnRead = new IOException("O arquivo está em uso.");

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Failed);
        result.Problems.ShouldContain(problem => problem.Contains("O arquivo está em uso.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestoreAsync_snapshot_que_nao_pode_ser_apagado_e_reportado_sem_falhar()
    {
        var test = new OrchestratorTest();
        test.Store.Content = SnapshotJson([Captured("a", order: 1, ModuleApplyState.Applied)]);
        test.Store.ThrowOnDelete = new IOException("O arquivo está em uso.");

        var result = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(RestoreStatus.Incomplete);
        test.StepsOf("revert").ShouldBe("a:revert");
        test.Environment.ShouldBe("original,original,original");
    }

    private static CapturedModule Captured(
        string moduleId,
        int order,
        ModuleApplyState applyState,
        bool survivesReboot = false) => new()
        {
            ModuleId = moduleId,
            Order = order,
            SurvivesReboot = survivesReboot,
            ApplyState = applyState,
            Snapshot = new ModuleSnapshot
            {
                PayloadVersion = 1,
                Payload = new JsonObject { ["value"] = FakeEnvironmentModule.Original },
            },
        };

    private static string SnapshotJson(
        IReadOnlyList<CapturedModule> modules,
        SessionState state = SessionState.Applied) => SessionSnapshotSerializer.Serialize(new SessionSnapshot
        {
            SchemaVersion = SnapshotSchema.CurrentVersion,
            SessionId = "sessao-1",
            Boot = new BootIdentity { Id = "boot-1", UptimeMs = 3_600_000 },
            CapturedAt = DateTimeOffset.UtcNow,
            State = state,
            ProfileId = "tv-4k",
            ClientName = "Sala",
            Modules = modules,
        });
}
