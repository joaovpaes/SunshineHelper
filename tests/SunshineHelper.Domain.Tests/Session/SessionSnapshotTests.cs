using System.Text.Json.Nodes;
using Shouldly;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Domain.Tests.Session;

public class SessionSnapshotTests
{
    private static readonly DateTimeOffset _capturedAt = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_grava_a_versao_atual_do_esquema_e_comeca_em_applying()
    {
        var snapshot = SessionSnapshot.Create(
            "sessao-1",
            Boot(),
            _capturedAt,
            [Module("display", order: 1)]);

        snapshot.SchemaVersion.ShouldBe(SnapshotSchema.CurrentVersion);
        snapshot.State.ShouldBe(SessionState.Applying);
        snapshot.Modules.ShouldHaveSingleItem().ApplyState.ShouldBe(ModuleApplyState.Pending);
    }

    [Fact]
    public void Create_guarda_o_perfil_e_o_cliente_quando_informados()
    {
        var snapshot = SessionSnapshot.Create(
            "sessao-1",
            Boot(),
            _capturedAt,
            [Module("display", order: 1)],
            profileId: "tv-4k",
            clientName: "Sala");

        snapshot.ProfileId.ShouldBe("tv-4k");
        snapshot.ClientName.ShouldBe("Sala");
    }

    [Fact]
    public void WithModuleState_troca_so_o_modulo_indicado()
    {
        var snapshot = SessionSnapshot.Create(
            "sessao-1",
            Boot(),
            _capturedAt,
            [Module("display", order: 1), Module("hdr", order: 2)]);

        var updated = snapshot.WithModuleState("hdr", ModuleApplyState.Applied);

        updated.Modules.Single(entry => entry.ModuleId == "hdr").ApplyState.ShouldBe(ModuleApplyState.Applied);
        updated.Modules.Single(entry => entry.ModuleId == "display").ApplyState.ShouldBe(ModuleApplyState.Pending);
    }

    [Fact]
    public void WithModuleState_nao_altera_o_snapshot_original()
    {
        var snapshot = SessionSnapshot.Create("sessao-1", Boot(), _capturedAt, [Module("display", order: 1)]);

        _ = snapshot.WithModuleState("display", ModuleApplyState.Applied);

        snapshot.Modules.ShouldHaveSingleItem().ApplyState.ShouldBe(ModuleApplyState.Pending);
    }

    [Fact]
    public void WithModuleState_preserva_o_payload_do_modulo()
    {
        var payload = new JsonObject { ["width"] = 3840 };
        var captured = Module("display", order: 1) with { Snapshot = new ModuleSnapshot { PayloadVersion = 2, Payload = payload } };
        var snapshot = SessionSnapshot.Create("sessao-1", Boot(), _capturedAt, [captured]);

        var updated = snapshot.WithModuleState("display", ModuleApplyState.Applied);

        var result = updated.Modules.ShouldHaveSingleItem();
        result.Snapshot.PayloadVersion.ShouldBe(2);
        result.Snapshot.Payload!["width"]!.GetValue<int>().ShouldBe(3840);
    }

    [Fact]
    public void WithModuleState_modulo_inexistente_lanca_ArgumentException()
    {
        var snapshot = SessionSnapshot.Create("sessao-1", Boot(), _capturedAt, [Module("display", order: 1)]);

        Should.Throw<ArgumentException>(() => snapshot.WithModuleState("fps", ModuleApplyState.Applied));
    }

    [Fact]
    public void WithState_troca_o_estado_da_sessao()
    {
        var snapshot = SessionSnapshot.Create("sessao-1", Boot(), _capturedAt, [Module("display", order: 1)]);

        snapshot.WithState(SessionState.RevertFailed).State.ShouldBe(SessionState.RevertFailed);
    }

    [Theory]
    [InlineData(ModuleApplyState.Applied, true)]
    [InlineData(ModuleApplyState.Applying, true)]
    [InlineData(ModuleApplyState.Failed, true)]
    [InlineData(ModuleApplyState.Pending, false)]
    [InlineData(ModuleApplyState.Skipped, false)]
    public void NeedsRevert_cobre_so_os_estados_que_mexeram_no_ambiente(ModuleApplyState applyState, bool expected)
    {
        var captured = Module("display", order: 1) with { ApplyState = applyState };

        captured.NeedsRevert.ShouldBe(expected);
    }

    private static BootIdentity Boot() => new() { Id = "boot-1", UptimeMs = 3_600_000 };

    private static CapturedModule Module(string moduleId, int order) => new()
    {
        ModuleId = moduleId,
        Order = order,
        SurvivesReboot = false,
        ApplyState = ModuleApplyState.Pending,
        Snapshot = new ModuleSnapshot { PayloadVersion = 1 },
    };
}
