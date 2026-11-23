using Shouldly;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Settings;

public class SettingsGeneralFieldsTests
{
    [Fact]
    public void Idioma_e_caminho_do_log_sobrevivem_a_gravar_e_ler()
    {
        var settings = AppSettings.CreateDefault() with
        {
            Language = "en",
            SunshineLogPath = @"C:\Sunshine\sunshine.log",
        };

        var roundTripped = SettingsSerializer.Deserialize(SettingsMigrator.Default.Migrate(
            SettingsSerializer.ParseRaw(SettingsSerializer.Serialize(settings))).Document);

        roundTripped.Language.ShouldBe("en");
        roundTripped.SunshineLogPath.ShouldBe(@"C:\Sunshine\sunshine.log");
    }

    [Fact]
    public void Arquivo_antigo_sem_idioma_continua_valido_e_segue_o_windows()
    {
        var json = SettingsSerializer.Serialize(AppSettings.CreateDefault());

        var loaded = SettingsSerializer.Deserialize(SettingsMigrator.Default.Migrate(
            SettingsSerializer.ParseRaw(json)).Document);

        loaded.Language.ShouldBeNull();
        SettingsValidator.Validate(loaded).IsValid.ShouldBeTrue();
    }
}
