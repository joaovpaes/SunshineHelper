using Shouldly;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class LauncherSettingsTests
{
    [Theory]
    [InlineData("Playnite.FullscreenApp", "Playnite.FullscreenApp")]
    [InlineData("Playnite.FullscreenApp.exe", "Playnite.FullscreenApp")]
    [InlineData("  Playnite.FullscreenApp.EXE  ", "Playnite.FullscreenApp")]
    public void NormalizeName_tira_espacos_e_exe(string input, string expected)
    {
        LauncherSettings.NormalizeName(input).ShouldBe(expected);
    }

    [Fact]
    public void Padrao_nao_fecha_nada()
    {
        var defaults = AppSettings.CreateDefault();

        defaults.Launcher.ProcessNames.ShouldBeEmpty();
        SettingsValidator.Validate(defaults).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Lista_com_launcher_comum_e_valida()
    {
        var settings = With(new LauncherSettings { ProcessNames = ["Playnite.FullscreenApp", "Playnite.DesktopApp.exe"] });

        SettingsValidator.Validate(settings).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("Sunshine")]
    [InlineData("SunshineHelper")]
    [InlineData("svchost")]
    [InlineData("winlogon")]
    public void Processo_do_windows_do_sunshine_ou_do_app_e_recusado(string name)
    {
        var settings = With(new LauncherSettings { ProcessNames = [name] });

        SettingsValidator.Validate(settings).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Program Files\Playnite\Playnite.FullscreenApp.exe")]
    [InlineData("Playnite*")]
    public void Nome_vazio_com_pasta_ou_curinga_e_recusado(string name)
    {
        var settings = With(new LauncherSettings { ProcessNames = [name] });

        SettingsValidator.Validate(settings).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Teto_de_espera_precisa_ser_positivo()
    {
        var settings = With(new LauncherSettings { CloseTimeoutMs = 0 });

        SettingsValidator.Validate(settings).IsValid.ShouldBeFalse();
    }

    private static AppSettings With(LauncherSettings launcher) => AppSettings.CreateDefault() with { Launcher = launcher };
}
