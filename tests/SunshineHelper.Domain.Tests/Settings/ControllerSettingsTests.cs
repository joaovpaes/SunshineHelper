using Shouldly;
using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class ControllerSettingsTests
{
    private const string Field = "combo";

    private static AppSettings With(ControllerSettings controller) => AppSettings.CreateDefault() with { Controller = controller };

    private static AppSettings WithCloseGame(CloseGameSettings closeGame) => With(new ControllerSettings { CloseGame = closeGame });

    private static ComboSettings Combo(int holdMs, params GamepadButtons[] buttons) => new() { Buttons = buttons, HoldMs = holdMs };

    [Fact]
    public void Padrao_e_valido_e_fechar_o_jogo_vem_desligado()
    {
        var defaults = AppSettings.CreateDefault();

        SettingsValidator.Validate(defaults).IsValid.ShouldBeTrue();
        defaults.Controller.CloseGame.Enabled.ShouldBeFalse();
        defaults.Controller.CloseGame.ForceWithoutConfirm.ShouldBeFalse();
        defaults.Controller.CloseGame.ReturnToLauncher.ShouldBeTrue();
        defaults.Controller.CloseGame.PoliteTimeoutMs.ShouldBe(5000);
        defaults.Controller.CloseGame.ProtectedNames.ShouldBeEmpty();
    }

    [Fact]
    public void Padrao_do_fechar_o_jogo_e_select_y_por_5_segundos()
    {
        var combo = new CloseGameSettings().Combo;

        combo.ToMask().ShouldBe(GamepadButtons.Back | GamepadButtons.Y);
        combo.HoldMs.ShouldBe(5000);
    }

    [Fact]
    public void Padrao_do_overlay_e_l3_r3_por_3_segundos()
    {
        var combo = new OverlaySettings().Combo;

        combo.ToMask().ShouldBe(GamepadButtons.LeftThumb | GamepadButtons.RightThumb);
        combo.HoldMs.ShouldBe(3000);
    }

    [Fact]
    public void ActiveCombos_so_tem_o_overlay_enquanto_fechar_o_jogo_esta_desligado()
    {
        var combos = new ControllerSettings().ActiveCombos();

        combos.Count.ShouldBe(1);
        combos[0].Id.ShouldBe(ControllerSettings.OverlayComboId);
        combos[0].Buttons.ShouldBe(GamepadButtons.LeftThumb | GamepadButtons.RightThumb);
        combos[0].Hold.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void ActiveCombos_fica_vazio_com_os_dois_desligados()
    {
        var controller = new ControllerSettings { Overlay = new OverlaySettings { Enabled = false } };

        controller.ActiveCombos().ShouldBeEmpty();
    }

    [Fact]
    public void O_overlay_vem_ligado_e_tomando_o_foco_de_fabrica()
    {
        var overlay = new OverlaySettings();

        overlay.Enabled.ShouldBeTrue();
        overlay.TakeFocus.ShouldBeTrue();
    }

    [Fact]
    public void ActiveCombos_traz_o_fechar_o_jogo_quando_ligado()
    {
        var controller = new ControllerSettings { CloseGame = new CloseGameSettings { Enabled = true } };

        var combos = controller.ActiveCombos();

        combos.Count.ShouldBe(2);
        combos[0].Id.ShouldBe(ControllerSettings.CloseGameComboId);
        combos[0].Buttons.ShouldBe(GamepadButtons.Back | GamepadButtons.Y);
        combos[0].Hold.ShouldBe(TimeSpan.FromSeconds(5));
        combos[1].Id.ShouldBe(ControllerSettings.OverlayComboId);
    }

    [Fact]
    public void ActiveCombos_serve_para_montar_o_detector()
    {
        var controller = new ControllerSettings { CloseGame = new CloseGameSettings { Enabled = true } };

        var detector = new ComboDetector(controller.ActiveCombos());

        detector.Update(0, GamepadButtons.Back | GamepadButtons.Y, TimeSpan.Zero).ShouldBeNull();
        var fired = detector.Update(0, GamepadButtons.Back | GamepadButtons.Y, TimeSpan.FromSeconds(5));
        fired.ShouldNotBeNull();
        fired.Id.ShouldBe(ControllerSettings.CloseGameComboId);
    }

    [Fact]
    public void ValidateCombo_aceita_de_2_a_4_botoes()
    {
        SettingsValidator.ValidateCombo(Combo(1000, GamepadButtons.A, GamepadButtons.B), Field).ShouldBeEmpty();
        SettingsValidator.ValidateCombo(
            Combo(1000, GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y), Field).ShouldBeEmpty();
    }

    [Fact]
    public void ValidateCombo_recusa_um_botao_so()
    {
        SettingsValidator.ValidateCombo(Combo(1000, GamepadButtons.A), Field)
            .ShouldContain(e => e.Contains("2 a 4"));
    }

    [Fact]
    public void ValidateCombo_recusa_cinco_botoes()
    {
        var combo = Combo(1000, GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y, GamepadButtons.LeftShoulder);

        SettingsValidator.ValidateCombo(combo, Field).ShouldContain(e => e.Contains("2 a 4"));
    }

    [Fact]
    public void ValidateCombo_recusa_start_com_select()
    {
        var combo = Combo(1000, GamepadButtons.Start, GamepadButtons.Back);

        SettingsValidator.ValidateCombo(combo, Field).ShouldContain(e => e.Contains("Start"));
    }

    [Fact]
    public void ValidateCombo_recusa_start_select_lb_rb_do_moonlight()
    {
        var combo = Combo(1000, GamepadButtons.Start, GamepadButtons.Back, GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder);

        SettingsValidator.ValidateCombo(combo, Field).ShouldContain(e => e.Contains("Start"));
    }

    [Fact]
    public void ValidateCombo_aceita_start_sem_select()
    {
        SettingsValidator.ValidateCombo(Combo(1000, GamepadButtons.Start, GamepadButtons.Y), Field).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10_001)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateCombo_recusa_tempo_fora_da_faixa(int holdMs)
    {
        SettingsValidator.ValidateCombo(Combo(holdMs, GamepadButtons.A, GamepadButtons.B), Field)
            .ShouldContain(e => e.Contains("holdMs"));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(10_000)]
    public void ValidateCombo_aceita_os_limites_do_tempo(int holdMs)
    {
        SettingsValidator.ValidateCombo(Combo(holdMs, GamepadButtons.A, GamepadButtons.B), Field).ShouldBeEmpty();
    }

    [Fact]
    public void ValidateCombo_recusa_botao_repetido()
    {
        SettingsValidator.ValidateCombo(Combo(1000, GamepadButtons.A, GamepadButtons.A), Field)
            .ShouldContain(e => e.Contains("repete"));
    }

    [Fact]
    public void ValidateCombo_recusa_guide_e_botoes_juntos_num_item()
    {
        var guide = Combo(1000, GamepadButtons.A, (GamepadButtons)0x0400);
        var merged = Combo(1000, GamepadButtons.A | GamepadButtons.B, GamepadButtons.X);

        SettingsValidator.ValidateCombo(guide, Field).ShouldContain(e => e.Contains("não é um botão aceito"));
        SettingsValidator.ValidateCombo(merged, Field).ShouldContain(e => e.Contains("não é um botão aceito"));
    }

    [Fact]
    public void ValidateCombo_nulo_ou_sem_lista_vira_erro()
    {
        SettingsValidator.ValidateCombo(null!, Field).ShouldNotBeEmpty();
        SettingsValidator.ValidateCombo(new ComboSettings { Buttons = null!, HoldMs = 1000 }, Field).ShouldNotBeEmpty();
    }

    [Fact]
    public void Combo_invalido_derruba_a_validacao_do_arquivo()
    {
        var settings = WithCloseGame(new CloseGameSettings { Combo = Combo(1000, GamepadButtons.Y) });

        var result = SettingsValidator.Validate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("controller.closeGame.combo"));
    }

    [Fact]
    public void Combo_do_overlay_invalido_derruba_a_validacao()
    {
        var settings = With(new ControllerSettings { Overlay = new OverlaySettings { Combo = Combo(100, GamepadButtons.A, GamepadButtons.B) } });

        SettingsValidator.Validate(settings).Errors.ShouldContain(e => e.Contains("controller.overlay.combo"));
    }

    [Fact]
    public void Fechar_o_jogo_e_overlay_com_os_mesmos_botoes_sao_recusados()
    {
        var same = Combo(1000, GamepadButtons.Y, GamepadButtons.Back);
        var settings = With(new ControllerSettings
        {
            CloseGame = new CloseGameSettings { Combo = same },
            Overlay = new OverlaySettings { Combo = Combo(2000, GamepadButtons.Back, GamepadButtons.Y) },
        });

        SettingsValidator.Validate(settings).Errors.ShouldContain(e => e.Contains("mesmos botões"));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(30_001)]
    public void Teto_do_pedido_fora_da_faixa_e_recusado(int timeoutMs)
    {
        var settings = WithCloseGame(new CloseGameSettings { PoliteTimeoutMs = timeoutMs });

        SettingsValidator.Validate(settings).Errors.ShouldContain(e => e.Contains("politeTimeoutMs"));
    }

    [Fact]
    public void Protegidos_aceitam_nomes_do_windows()
    {
        var settings = WithCloseGame(new CloseGameSettings { ProtectedNames = ["explorer", "Discord.exe"] });

        SettingsValidator.Validate(settings).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"C:\Games\game.exe")]
    [InlineData("game*")]
    public void Protegidos_recusam_nome_vazio_pasta_ou_curinga(string name)
    {
        var settings = WithCloseGame(new CloseGameSettings { ProtectedNames = [name] });

        SettingsValidator.Validate(settings).Errors.ShouldContain(e => e.Contains("protectedNames"));
    }

    [Fact]
    public void Secoes_nulas_viram_erro_e_nao_excecao()
    {
        SettingsValidator.Validate(With(null!)).IsValid.ShouldBeFalse();
        SettingsValidator.Validate(With(new ControllerSettings { CloseGame = null! })).IsValid.ShouldBeFalse();
        SettingsValidator.Validate(WithCloseGame(new CloseGameSettings { ProtectedNames = null! })).IsValid.ShouldBeFalse();
    }
}
