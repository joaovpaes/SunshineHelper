using System.Text.Json.Nodes;
using Shouldly;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

public class SessionSnapshotSerializerTests
{
    private static readonly DateTimeOffset _capturedAt = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Serializar_e_desserializar_preserva_o_snapshot()
    {
        var original = Snapshot();

        var result = SessionSnapshotSerializer.Deserialize(SessionSnapshotSerializer.Serialize(original));

        result.SchemaVersion.ShouldBe(SnapshotSchema.CurrentVersion);
        result.SessionId.ShouldBe("sessao-1");
        result.Boot.Id.ShouldBe("boot-1");
        result.Boot.UptimeMs.ShouldBe(3_600_000);
        result.CapturedAt.ShouldBe(_capturedAt);
        result.State.ShouldBe(SessionState.Applied);
        result.ProfileId.ShouldBe("tv-4k");
        result.ClientName.ShouldBe("Sala");
        result.Modules.Count.ShouldBe(2);
    }

    [Fact]
    public void Serializar_e_desserializar_preserva_o_payload_opaco_de_cada_modulo()
    {
        var result = SessionSnapshotSerializer.Deserialize(SessionSnapshotSerializer.Serialize(Snapshot()));

        var display = result.Modules.Single(entry => entry.ModuleId == "display");
        display.Snapshot.PayloadVersion.ShouldBe(2);
        display.Snapshot.Payload!["width"]!.GetValue<int>().ShouldBe(3840);
        display.Snapshot.Payload["height"]!.GetValue<int>().ShouldBe(2160);

        var fps = result.Modules.Single(entry => entry.ModuleId == "fps");
        fps.Snapshot.Payload.ShouldBeNull();
        fps.SurvivesReboot.ShouldBeTrue();
    }

    [Fact]
    public void Serializar_e_desserializar_preserva_o_estado_de_cada_modulo()
    {
        var result = SessionSnapshotSerializer.Deserialize(SessionSnapshotSerializer.Serialize(Snapshot()));

        result.Modules.Single(entry => entry.ModuleId == "display").ApplyState.ShouldBe(ModuleApplyState.Applied);
        result.Modules.Single(entry => entry.ModuleId == "fps").ApplyState.ShouldBe(ModuleApplyState.Skipped);
    }

    [Fact]
    public void Serializar_usa_camelCase_e_escreve_os_estados_como_texto()
    {
        var json = SessionSnapshotSerializer.Serialize(Snapshot());

        json.ShouldContain("\"schemaVersion\"");
        json.ShouldContain("\"survivesReboot\"");
        json.ShouldContain("\"applyState\": \"applied\"");
        json.ShouldContain("\"state\": \"applied\"");
    }

    [Fact]
    public void Serializar_nao_grava_campos_calculados()
    {
        var json = SessionSnapshotSerializer.Serialize(Snapshot());

        json.ShouldNotContain("needsRevert");
    }

    [Fact]
    public void Desserializar_json_invalido_lanca_SnapshotFormatException() =>
        Should.Throw<SnapshotFormatException>(() => SessionSnapshotSerializer.Deserialize("{ isso não é json"));

    [Fact]
    public void Desserializar_json_nulo_lanca_SnapshotFormatException() =>
        Should.Throw<SnapshotFormatException>(() => SessionSnapshotSerializer.Deserialize("null"));

    [Fact]
    public void Desserializar_sem_campo_obrigatorio_lanca_SnapshotFormatException() =>
        Should.Throw<SnapshotFormatException>(
            () => SessionSnapshotSerializer.Deserialize("{\"schemaVersion\": 1}"));

    [Fact]
    public void Desserializar_versao_mais_nova_que_a_suportada_lanca_SnapshotFormatException()
    {
        var json = SessionSnapshotSerializer.Serialize(
            Snapshot() with { SchemaVersion = SnapshotSchema.CurrentVersion + 1 });

        var exception = Should.Throw<SnapshotFormatException>(() => SessionSnapshotSerializer.Deserialize(json));

        exception.Message.ShouldContain("mais nova");
    }

    [Fact]
    public void Desserializar_versao_sem_caminho_de_leitura_lanca_SnapshotFormatException()
    {
        var json = SessionSnapshotSerializer.Serialize(Snapshot() with { SchemaVersion = 0 });

        var exception = Should.Throw<SnapshotFormatException>(() => SessionSnapshotSerializer.Deserialize(json));

        exception.Message.ShouldContain("sem caminho de leitura");
    }

    private static SessionSnapshot Snapshot() => new()
    {
        SchemaVersion = SnapshotSchema.CurrentVersion,
        SessionId = "sessao-1",
        Boot = new BootIdentity { Id = "boot-1", UptimeMs = 3_600_000 },
        CapturedAt = _capturedAt,
        State = SessionState.Applied,
        ProfileId = "tv-4k",
        ClientName = "Sala",
        Modules =
        [
            new CapturedModule
            {
                ModuleId = "display",
                Order = 1,
                SurvivesReboot = false,
                ApplyState = ModuleApplyState.Applied,
                Snapshot = new ModuleSnapshot
                {
                    PayloadVersion = 2,
                    Payload = new JsonObject { ["width"] = 3840, ["height"] = 2160 },
                },
            },
            new CapturedModule
            {
                ModuleId = "fps",
                Order = 2,
                SurvivesReboot = true,
                ApplyState = ModuleApplyState.Skipped,
                Snapshot = new ModuleSnapshot { PayloadVersion = 1 },
            },
        ],
    };
}
