using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.CloseGame;

public class CloseGameProtectionTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 27, 20, 0, 0, TimeSpan.Zero);

    private static readonly AppSettings _settings = AppSettings.CreateDefault() with
    {
        Launcher = new LauncherSettings { ProcessNames = ["Playnite.FullscreenApp.exe"] },
        Controller = new ControllerSettings { CloseGame = new CloseGameSettings { ProtectedNames = ["obs64"] } },
    };

    [Theory]
    [InlineData("playnite.fullscreenapp", CloseGameProtectionReason.Launcher)]
    [InlineData("Playnite.FullscreenApp.EXE", CloseGameProtectionReason.Launcher)]
    [InlineData("OBS64.exe", CloseGameProtectionReason.UserList)]
    [InlineData("csrss", CloseGameProtectionReason.Fixed)]
    [InlineData("applicationframehost.exe", CloseGameProtectionReason.Fixed)]
    [InlineData("", CloseGameProtectionReason.Fixed)]
    [InlineData("Hades2", CloseGameProtectionReason.None)]
    public void CheckName_compara_sem_exe_e_sem_maiusculas(string name, CloseGameProtectionReason expected)
    {
        CloseGameProtection.CheckName(name, _settings).ShouldBe(expected);
    }

    [Fact]
    public void Inicio_um_segundo_antes_da_sessao_ainda_vale()
    {
        var almost = new ProcessIdentity(1, "Hades2", _start.AddMilliseconds(-900));
        var before = new ProcessIdentity(2, "Hades2", _start.AddSeconds(-2));

        CloseGameProtection.Check(almost, _start, _settings).ShouldBe(CloseGameProtectionReason.None);
        CloseGameProtection.Check(before, _start, _settings).ShouldBe(CloseGameProtectionReason.StartedBeforeSession);
    }

    [Fact]
    public void Nome_protegido_ganha_de_horario()
    {
        var launcher = new ProcessIdentity(1, "Playnite.FullscreenApp", _start.AddMinutes(1));

        CloseGameProtection.Check(launcher, _start, _settings).ShouldBe(CloseGameProtectionReason.Launcher);
    }

    [Fact]
    public void Argumentos_nulos_sao_recusados()
    {
        Should.Throw<ArgumentNullException>(() => CloseGameProtection.Check(null!, _start, _settings));
        Should.Throw<ArgumentNullException>(() => CloseGameProtection.CheckName(null!, _settings));
        Should.Throw<ArgumentNullException>(() => CloseGameProtection.CheckName("x", null!));
    }
}
