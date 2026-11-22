using System.Text.Json.Nodes;
using Shouldly;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class SettingsMigratorTests
{
    [Fact]
    public void Migrate_documento_ja_na_versao_atual_nao_muda_nada()
    {
        var document = new JsonObject { ["schemaVersion"] = SettingsSchema.CurrentVersion };

        var result = SettingsMigrator.Default.Migrate(document);

        result.Migrated.ShouldBeFalse();
        result.Document["schemaVersion"]!.GetValue<int>().ShouldBe(SettingsSchema.CurrentVersion);
    }

    [Fact]
    public void Migrate_documento_sem_schemaVersion_e_tratado_como_versao_0_e_migrado()
    {
        var document = new JsonObject
        {
            ["profiles"] = new JsonArray(),
        };

        var result = SettingsMigrator.Default.Migrate(document);

        result.Migrated.ShouldBeTrue();
        result.Document["schemaVersion"]!.GetValue<int>().ShouldBe(SettingsSchema.CurrentVersion);
    }

    [Fact]
    public void Migrate_adiciona_timing_padrao_quando_ausente()
    {
        var document = new JsonObject();

        var result = SettingsMigrator.Default.Migrate(document);

        var timing = result.Document["timing"].ShouldBeOfType<JsonObject>();
        timing["applyTargetMs"]!.GetValue<int>().ShouldBe(new TimingSettings().ApplyTargetMs);
        timing["barrierDefaultMs"]!.GetValue<int>().ShouldBe(new TimingSettings().BarrierDefaultMs);
        timing["barrierCeilingMs"]!.GetValue<int>().ShouldBe(new TimingSettings().BarrierCeilingMs);
    }

    [Fact]
    public void Migrate_preserva_timing_ja_presente()
    {
        var document = new JsonObject
        {
            ["timing"] = new JsonObject { ["applyTargetMs"] = 5_000 },
        };

        var result = SettingsMigrator.Default.Migrate(document);

        var timing = result.Document["timing"].ShouldBeOfType<JsonObject>();
        timing["applyTargetMs"]!.GetValue<int>().ShouldBe(5_000);
    }

    [Fact]
    public void Migrate_adiciona_lista_de_perfis_vazia_quando_ausente()
    {
        var document = new JsonObject();

        var result = SettingsMigrator.Default.Migrate(document);

        result.Document["profiles"].ShouldBeOfType<JsonArray>();
    }

    [Fact]
    public void Migrate_sem_caminho_de_migracao_lanca_SettingsFormatException()
    {
        var migrator = new SettingsMigrator([]);
        var document = new JsonObject { ["schemaVersion"] = 0 };

        Should.Throw<SettingsFormatException>(() => migrator.Migrate(document));
    }

    [Fact]
    public void Migrate_nao_modifica_o_documento_original()
    {
        var original = new JsonObject();

        SettingsMigrator.Default.Migrate(original);

        original.ContainsKey("schemaVersion").ShouldBeFalse();
    }
}
