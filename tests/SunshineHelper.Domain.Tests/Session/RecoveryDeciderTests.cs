using Shouldly;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Domain.Tests.Session;

public class RecoveryDeciderTests
{
    private static readonly DateTimeOffset _capturedAt = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_mesmo_boot_reverte_tudo_que_foi_aplicado_na_ordem_inversa()
    {
        var snapshot = Snapshot(
        [
            Module("display", order: 1, ModuleApplyState.Applied),
            Module("hdr", order: 2, ModuleApplyState.Applied),
            Module("fps", order: 3, ModuleApplyState.Applied, survivesReboot: true),
        ]);

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-1", 7_200_000), _capturedAt.AddMinutes(10));

        plan.RebootDetected.ShouldBeFalse();
        Ids(plan.ModulesToRevert).ShouldBe("fps,hdr,display");
        plan.ModulesLeftAlone.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_nao_reverte_modulo_que_nunca_mexeu_no_ambiente()
    {
        var snapshot = Snapshot(
        [
            Module("display", order: 1, ModuleApplyState.Skipped),
            Module("hdr", order: 2, ModuleApplyState.Pending),
            Module("fps", order: 3, ModuleApplyState.Applied),
        ]);

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-1", 7_200_000), _capturedAt.AddMinutes(10));

        Ids(plan.ModulesToRevert).ShouldBe("fps");
        Ids(plan.ModulesLeftAlone).ShouldBe("hdr,display");
    }

    [Theory]
    [InlineData(ModuleApplyState.Applying)]
    [InlineData(ModuleApplyState.Failed)]
    public void Decide_reverte_modulo_que_pode_ter_sido_aplicado_pela_metade(ModuleApplyState applyState)
    {
        var snapshot = Snapshot([Module("display", order: 1, applyState)]);

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-1", 7_200_000), _capturedAt.AddMinutes(10));

        Ids(plan.ModulesToRevert).ShouldBe("display");
    }

    [Fact]
    public void Decide_apos_reinicio_reverte_so_o_que_sobrevive_ao_reinicio()
    {
        var snapshot = Snapshot(
        [
            Module("display", order: 1, ModuleApplyState.Applied),
            Module("hdr", order: 2, ModuleApplyState.Applied),
            Module("fps", order: 3, ModuleApplyState.Applied, survivesReboot: true),
        ],
        Boot("boot-1", 3_600_000));

        // O PC está ligado há menos tempo do que quando o snapshot foi gravado:
        // só pode ter reiniciado.
        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-2", 60_000), _capturedAt.AddMinutes(10));

        plan.RebootDetected.ShouldBeTrue();
        Ids(plan.ModulesToRevert).ShouldBe("fps");
        Ids(plan.ModulesLeftAlone).ShouldBe("hdr,display");
    }

    [Fact]
    public void Decide_sem_identidade_de_boot_assume_mesmo_boot_e_reverte_tudo()
    {
        var snapshot = Snapshot(
        [
            Module("display", order: 1, ModuleApplyState.Applied),
            Module("fps", order: 2, ModuleApplyState.Applied, survivesReboot: true),
        ]);

        var plan = RecoveryDecider.Decide(snapshot, current: null, _capturedAt.AddMinutes(10));

        plan.RebootDetected.ShouldBeFalse();
        Ids(plan.ModulesToRevert).ShouldBe("fps,display");
    }

    [Fact]
    public void Decide_id_diferente_com_uptime_coerente_assume_mesmo_boot()
    {
        // Relógio do sistema ajustado: o identificador muda sem o PC ter reiniciado.
        var snapshot = Snapshot([Module("display", order: 1, ModuleApplyState.Applied)], Boot("boot-1", 3_600_000));

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-2", 4_200_000), _capturedAt.AddMinutes(10));

        plan.RebootDetected.ShouldBeFalse();
        Ids(plan.ModulesToRevert).ShouldBe("display");
    }

    [Fact]
    public void Decide_id_diferente_e_uptime_menor_que_o_tempo_decorrido_detecta_reinicio()
    {
        // Ligado há 2 min, mas o snapshot é de 30 min atrás: não ficou ligado o tempo todo.
        var snapshot = Snapshot([Module("display", order: 1, ModuleApplyState.Applied)], Boot("boot-1", 60_000));

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-2", 120_000), _capturedAt.AddMinutes(30));

        plan.RebootDetected.ShouldBeTrue();
        plan.ModulesToRevert.ShouldBeEmpty();
        Ids(plan.ModulesLeftAlone).ShouldBe("display");
    }

    [Fact]
    public void Decide_relogio_para_tras_nao_detecta_reinicio()
    {
        var snapshot = Snapshot([Module("display", order: 1, ModuleApplyState.Applied)], Boot("boot-1", 60_000));

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-2", 120_000), _capturedAt.AddMinutes(-5));

        plan.RebootDetected.ShouldBeFalse();
        Ids(plan.ModulesToRevert).ShouldBe("display");
    }

    [Fact]
    public void Decide_plano_vazio_quando_nada_precisa_voltar()
    {
        var snapshot = Snapshot(
        [
            Module("display", order: 1, ModuleApplyState.Skipped),
            Module("hdr", order: 2, ModuleApplyState.Skipped),
        ]);

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-1", 7_200_000), _capturedAt.AddMinutes(10));

        plan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Decide_mantem_o_estado_da_sessao_no_plano()
    {
        var snapshot = Snapshot(
            [Module("display", order: 1, ModuleApplyState.Applied)],
            state: SessionState.RevertFailed);

        var plan = RecoveryDecider.Decide(snapshot, Boot("boot-1", 7_200_000), _capturedAt.AddMinutes(10));

        plan.State.ShouldBe(SessionState.RevertFailed);
        plan.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Decide_snapshot_nulo_lanca_ArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => RecoveryDecider.Decide(null!, Boot("boot-1", 1), _capturedAt));

    private static string Ids(IEnumerable<CapturedModule> modules) =>
        string.Join(",", modules.Select(entry => entry.ModuleId));

    private static BootIdentity Boot(string id, long uptimeMs) => new() { Id = id, UptimeMs = uptimeMs };

    private static CapturedModule Module(
        string moduleId,
        int order,
        ModuleApplyState applyState,
        bool survivesReboot = false) => new()
        {
            ModuleId = moduleId,
            Order = order,
            SurvivesReboot = survivesReboot,
            ApplyState = applyState,
            Snapshot = new ModuleSnapshot { PayloadVersion = 1 },
        };

    private static SessionSnapshot Snapshot(
        IReadOnlyList<CapturedModule> modules,
        BootIdentity? boot = null,
        SessionState state = SessionState.Applied) => new()
        {
            SchemaVersion = SnapshotSchema.CurrentVersion,
            SessionId = "sessao-1",
            Boot = boot ?? Boot("boot-1", 3_600_000),
            CapturedAt = _capturedAt,
            State = state,
            Modules = modules,
        };
}
