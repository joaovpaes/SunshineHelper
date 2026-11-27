using Shouldly;
using SunshineHelper.Application.Settings;
using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Settings;

public class ControllerSettingsJsonTests
{
    [Fact]
    public void Arquivo_sem_controller_carrega_os_padroes()
    {
        var settings = SettingsSerializer.Deserialize(SettingsSerializer.ParseRaw("""{ "schemaVersion": 1 }"""));

        settings.Controller.CloseGame.Enabled.ShouldBeFalse();
        settings.Controller.CloseGame.Combo.ToMask().ShouldBe(GamepadButtons.Back | GamepadButtons.Y);
        SettingsValidator.Validate(settings).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Botoes_sao_gravados_pelo_nome()
    {
        string json = SettingsSerializer.Serialize(AppSettings.CreateDefault());

        json.ShouldContain("\"controller\"");
        json.ShouldContain("\"closeGame\"");
        json.ShouldContain("\"back\"");
        json.ShouldContain("\"leftThumb\"");
        json.ShouldNotContain("\"mask\"");
    }

    [Fact]
    public void Ida_e_volta_mantem_o_que_o_usuario_escolheu()
    {
        var original = AppSettings.CreateDefault() with
        {
            Controller = new ControllerSettings
            {
                CloseGame = new CloseGameSettings
                {
                    Enabled = true,
                    Combo = new ComboSettings { Buttons = [GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder, GamepadButtons.Y], HoldMs = 3000 },
                    PoliteTimeoutMs = 8000,
                    ForceWithoutConfirm = true,
                    ReturnToLauncher = false,
                    ProtectedNames = ["Discord"],
                },
            },
        };

        var loaded = SettingsSerializer.Deserialize(SettingsSerializer.ParseRaw(SettingsSerializer.Serialize(original)));

        var closeGame = loaded.Controller.CloseGame;
        closeGame.Enabled.ShouldBeTrue();
        closeGame.Combo.ToMask().ShouldBe(GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y);
        closeGame.Combo.HoldMs.ShouldBe(3000);
        closeGame.PoliteTimeoutMs.ShouldBe(8000);
        closeGame.ForceWithoutConfirm.ShouldBeTrue();
        closeGame.ReturnToLauncher.ShouldBeFalse();
        closeGame.ProtectedNames.Count.ShouldBe(1);
        closeGame.ProtectedNames[0].ShouldBe("Discord");
    }

    [Fact]
    public void Secao_parcial_completa_com_os_padroes()
    {
        var settings = SettingsSerializer.Deserialize(SettingsSerializer.ParseRaw(
            """{ "schemaVersion": 1, "controller": { "closeGame": { "enabled": true } } }"""));

        settings.Controller.CloseGame.Enabled.ShouldBeTrue();
        settings.Controller.CloseGame.Combo.HoldMs.ShouldBe(5000);
        settings.Controller.Overlay.Combo.HoldMs.ShouldBe(3000);
    }
}
