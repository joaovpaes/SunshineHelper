using Shouldly;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

public class SessionOrchestratorApplyTests
{
    [Fact]
    public async Task ApplyAsync_captura_tudo_antes_de_aplicar_qualquer_modulo()
    {
        var test = new OrchestratorTest();

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        string.Join(",", test.Log).ShouldBe(
            "a:capture,b:capture,c:capture," +
            "store:write," +
            "store:write,a:apply,store:write," +
            "store:write,b:apply,store:write," +
            "store:write,c:apply,store:write," +
            "store:write");
    }

    [Fact]
    public async Task ApplyAsync_aplica_os_modulos_na_ordem_declarada()
    {
        var test = new OrchestratorTest(orders: [3, 1, 2]);

        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        test.StepsOf("apply").ShouldBe("b:apply,c:apply,a:apply");
    }

    [Fact]
    public async Task ApplyAsync_recusa_iniciar_quando_ja_existe_snapshot()
    {
        var test = new OrchestratorTest();
        test.Store.Content = "{}";

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Refused);
        test.Log.ShouldBeEmpty();
        test.Store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyAsync_falha_na_captura_nao_grava_nada_nem_aplica_nada()
    {
        var test = new OrchestratorTest();
        test.Modules[1].CaptureThrows = new InvalidOperationException("A placa de vídeo não respondeu.");

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        result.EnvironmentRestored.ShouldBeTrue();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("A placa de vídeo não respondeu.");
        test.Store.WriteCount.ShouldBe(0);
        test.Environment.ShouldBe("original,original,original");
    }

    [Fact]
    public async Task ApplyAsync_falha_ao_gravar_o_snapshot_nao_aplica_nada()
    {
        var test = new OrchestratorTest();
        test.Store.FailWriteAfter = 0;

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        result.EnvironmentRestored.ShouldBeTrue();
        test.StepsOf("apply").ShouldBeEmpty();
        test.Environment.ShouldBe("original,original,original");
    }

    [Fact]
    public async Task ApplyAsync_marca_o_modulo_como_applying_antes_de_mexer_no_ambiente()
    {
        var test = new OrchestratorTest();
        string? onDiskDuringApply = null;
        test.Modules[1].WhileApplying = () => onDiskDuringApply = test.Store.Content;

        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        onDiskDuringApply.ShouldNotBeNull();
        var snapshot = SessionSnapshotSerializer.Deserialize(onDiskDuringApply!);
        snapshot.Modules.Single(entry => entry.ModuleId == "b").ApplyState.ShouldBe(ModuleApplyState.Applying);
        snapshot.Modules.Single(entry => entry.ModuleId == "a").ApplyState.ShouldBe(ModuleApplyState.Applied);
        snapshot.Modules.Single(entry => entry.ModuleId == "c").ApplyState.ShouldBe(ModuleApplyState.Pending);
    }

    [Theory]
    [InlineData(0, "original,applied,applied")]
    [InlineData(1, "applied,original,applied")]
    [InlineData(2, "applied,applied,original")]
    public async Task ApplyAsync_modulo_que_falha_e_desfeito_sozinho_e_os_outros_continuam(int failingModule, string expected)
    {
        var test = new OrchestratorTest();
        test.Modules[failingModule].ApplyFails = "O módulo desistiu.";
        test.Modules[failingModule].ApplyFailsHalfway = true;

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        result.Problems.ShouldContain(problem => problem.Contains("O módulo desistiu.", StringComparison.Ordinal));
        test.Environment.ShouldBe(expected);
        var snapshot = SessionSnapshotSerializer.Deserialize(test.Store.Content.ShouldNotBeNull());
        snapshot.State.ShouldBe(SessionState.Applied);
        snapshot.Modules.Single(entry => entry.ModuleId == test.Modules[failingModule].Id).ApplyState.ShouldBe(ModuleApplyState.RolledBack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ApplyAsync_modulo_que_lanca_excecao_vira_aviso(int failingModule)
    {
        var test = new OrchestratorTest();
        test.Modules[failingModule].ApplyThrows = new InvalidOperationException("Erro do Windows.");

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        result.Problems.ShouldContain(problem => problem.Contains("Erro do Windows.", StringComparison.Ordinal));
        test.Modules[failingModule].Value.ShouldBe(FakeEnvironmentModule.Original);
    }

    [Fact]
    public async Task ApplyAsync_desfaz_na_hora_so_o_modulo_que_falhou()
    {
        var test = new OrchestratorTest();
        test.Modules[1].ApplyFails = "O módulo desistiu.";

        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        test.StepsOf("apply").ShouldBe("a:apply,b:apply,c:apply");
        test.StepsOf("revert").ShouldBe("b:revert");
    }

    [Fact]
    public async Task No_fim_da_sessao_tudo_volta_e_o_modulo_que_falhou_nao_e_desfeito_de_novo()
    {
        var test = new OrchestratorTest();
        test.Modules[1].ApplyFails = "Sem permissão.";
        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);
        test.Log.Clear();

        // A second revert of a module that cannot work here would fail and keep the
        // snapshot for nothing.
        test.Modules[1].RevertFails = "Sem permissão.";
        var restore = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        restore.Status.ShouldBe(RestoreStatus.Restored);
        test.StepsOf("revert").ShouldBe("c:revert,a:revert");
        test.Environment.ShouldBe("original,original,original");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_quando_todos_falham_a_sessao_segue_sem_nada_e_termina_limpa()
    {
        var test = new OrchestratorTest();
        foreach (var module in test.Modules)
        {
            module.ApplyFails = "Não.";
        }

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);
        var restore = await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        result.Problems.Count.ShouldBe(3);
        restore.Status.ShouldBe(RestoreStatus.Restored);
        test.Environment.ShouldBe("original,original,original");
        test.Store.Content.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_modulo_sem_nada_a_fazer_nao_e_desfeito()
    {
        var test = new OrchestratorTest();
        test.Modules[1].ApplyDoesNothing = true;
        test.Modules[2].ApplyFails = "O módulo desistiu.";

        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);
        await test.Orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        test.StepsOf("revert").ShouldBe("c:revert,a:revert");
    }

    [Fact]
    public async Task ApplyAsync_modulo_que_falha_e_nao_volta_desfaz_tudo_e_mantem_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Modules[2].ApplyFails = "O módulo desistiu.";
        test.Modules[2].ApplyFailsHalfway = true;
        test.Modules[2].RevertFails = "Não deu para voltar.";

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        result.EnvironmentRestored.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("Não deu para voltar.");
        test.Modules[0].Value.ShouldBe(FakeEnvironmentModule.Original);
        test.Modules[1].Value.ShouldBe(FakeEnvironmentModule.Original);
        SessionSnapshotSerializer.Deserialize(test.Store.Content.ShouldNotBeNull()).State.ShouldBe(SessionState.RevertFailed);
    }

    [Fact]
    public async Task ApplyAsync_modulo_que_lanca_ao_desfazer_tambem_desfaz_tudo_e_mantem_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Modules[2].ApplyFails = "O módulo desistiu.";
        test.Modules[2].RevertThrows = new InvalidOperationException("Erro ao restaurar.");

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        result.EnvironmentRestored.ShouldBeFalse();
        test.Store.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_modulo_que_volta_sozinho_mas_outro_falha_no_rollback_mantem_o_snapshot()
    {
        var test = new OrchestratorTest();
        test.Modules[2].ApplyFails = "O módulo desistiu.";
        test.Modules[2].RevertFails = "Não deu para voltar.";
        test.Modules[0].RevertFails = "Também não.";

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.EnvironmentRestored.ShouldBeFalse();
        result.Problems.ShouldContain(problem => problem.Contains("Também não.", StringComparison.Ordinal));
        test.Store.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_cancelado_no_meio_desfaz_tudo_como_antes()
    {
        var test = new OrchestratorTest();
        using var cancellation = new CancellationTokenSource();
        test.Modules[1].WhileApplying = cancellation.Cancel;
        test.Modules[1].ApplyThrows = new OperationCanceledException();

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), cancellation.Token);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        test.StepsOf("apply").ShouldBe("a:apply,b:apply");
        test.Environment.ShouldBe("original,original,original");
    }

    [Fact]
    public async Task ApplyAsync_falha_ao_gravar_antes_de_um_modulo_desfaz_os_anteriores()
    {
        var test = new OrchestratorTest();

        // Grava: 1 inicial, 2 "a aplicando", 3 "a aplicado"; a quarta (b aplicando) falha.
        test.Store.FailWriteAfter = 3;

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Failed);
        test.StepsOf("apply").ShouldBe("a:apply");
        test.StepsOf("revert").ShouldBe("a:revert");
        test.Environment.ShouldBe("original,original,original");
    }

    [Fact]
    public async Task ApplyAsync_sem_identidade_de_boot_ainda_aplica()
    {
        var test = new OrchestratorTest();
        test.Boot.Current = null;

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        SessionSnapshotSerializer.Deserialize(test.Store.Content!).Boot.Id.ShouldBe(BootIdentity.Unknown.Id);
    }

    [Fact]
    public async Task ApplyAsync_provedor_de_boot_que_lanca_nao_derruba_a_sessao()
    {
        var test = new OrchestratorTest();
        test.Boot.Throws = new InvalidOperationException("Não deu para ler o boot.");

        var result = await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
    }

    [Fact]
    public async Task ApplyAsync_guarda_o_perfil_e_o_cliente_no_snapshot()
    {
        var test = new OrchestratorTest();

        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        var snapshot = SessionSnapshotSerializer.Deserialize(test.Store.Content!);
        snapshot.ProfileId.ShouldBe("tv-4k");
        snapshot.ClientName.ShouldBe("Sala");
        snapshot.State.ShouldBe(SessionState.Applied);
    }
}
