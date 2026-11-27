using Shouldly;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Domain.Tests.Gamepad;

public class ComboDetectorTests
{
    private const GamepadButtons SelectY = GamepadButtons.Back | GamepadButtons.Y;
    private const GamepadButtons L3R3 = GamepadButtons.LeftThumb | GamepadButtons.RightThumb;

    private static readonly ComboDefinition _closeGame = new("closeGame", SelectY, TimeSpan.FromMilliseconds(5000));
    private static readonly ComboDefinition _overlay = new("overlay", L3R3, TimeSpan.FromMilliseconds(3000));

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static ComboDetector Detector() => new([_closeGame, _overlay]);

    [Fact]
    public void Dispara_uma_vez_quando_o_tempo_de_segurar_chega()
    {
        var detector = Detector();

        detector.Update(0, SelectY, Ms(0)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(4999)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(5000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Continuar_segurando_nao_dispara_de_novo()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(0, SelectY, Ms(5000)).ShouldBe(_closeGame);

        detector.Update(0, SelectY, Ms(10_000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(60_000)).ShouldBeNull();
    }

    [Fact]
    public void So_rearma_depois_de_soltar_todos_os_botoes_do_combo()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(0, SelectY, Ms(5000)).ShouldBe(_closeGame);

        // Soltou só o Y e apertou de novo, com o Select ainda apertado: continua esperando.
        detector.Update(0, GamepadButtons.Back, Ms(5100)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(5200)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(20_000)).ShouldBeNull();

        // Soltou tudo: a próxima segurada conta do zero.
        detector.Update(0, GamepadButtons.None, Ms(21_000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(22_000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(26_999)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(27_000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Botao_a_mais_cancela_e_a_contagem_recomeca()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(0, SelectY | GamepadButtons.X, Ms(3000)).ShouldBeNull();

        // Voltou ao combo exato: conta a partir daqui, não do início.
        detector.Update(0, SelectY, Ms(6000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(10_999)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(11_000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Soltar_um_botao_antes_do_tempo_cancela()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(0, GamepadButtons.Y, Ms(2000)).ShouldBeNull();

        detector.Update(0, SelectY, Ms(3000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(5000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(8000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Botao_isolado_ou_combinacao_desconhecida_nao_dispara()
    {
        var detector = Detector();

        detector.Update(0, GamepadButtons.Y, Ms(0)).ShouldBeNull();
        detector.Update(0, GamepadButtons.Y, Ms(10_000)).ShouldBeNull();
        detector.Update(0, GamepadButtons.A | GamepadButtons.B, Ms(20_000)).ShouldBeNull();
        detector.Update(0, GamepadButtons.A | GamepadButtons.B, Ms(40_000)).ShouldBeNull();
    }

    [Fact]
    public void Controle_desconectado_cancela_a_contagem()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Disconnect(0);

        detector.Update(0, SelectY, Ms(5000)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(10_000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Cada_controle_tem_o_seu_estado()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(1, GamepadButtons.A, Ms(1000)).ShouldBeNull();
        detector.Update(1, SelectY, Ms(2000)).ShouldBeNull();

        detector.Update(0, SelectY, Ms(5000)).ShouldBe(_closeGame);
        detector.Update(1, SelectY, Ms(6999)).ShouldBeNull();
        detector.Update(1, SelectY, Ms(7000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Cada_combo_usa_o_proprio_tempo()
    {
        var detector = Detector();
        detector.Update(2, L3R3, Ms(0)).ShouldBeNull();
        detector.Update(2, L3R3, Ms(2999)).ShouldBeNull();
        detector.Update(2, L3R3, Ms(3000)).ShouldBe(_overlay);
    }

    [Fact]
    public void Trocar_direto_de_um_combo_para_outro_comeca_a_contar_o_outro()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(0, L3R3, Ms(1000)).ShouldBeNull();

        detector.Update(0, L3R3, Ms(3999)).ShouldBeNull();
        detector.Update(0, L3R3, Ms(4000)).ShouldBe(_overlay);
    }

    [Fact]
    public void Botao_guide_e_bits_desconhecidos_sao_ignorados()
    {
        var detector = Detector();
        var withGuide = SelectY | (GamepadButtons)0x0400;

        detector.Update(0, withGuide, Ms(0)).ShouldBeNull();
        detector.Update(0, withGuide, Ms(5000)).ShouldBe(_closeGame);
    }

    [Fact]
    public void Reset_esquece_todos_os_controles()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(0));
        detector.Update(3, L3R3, Ms(0));
        detector.Reset();

        detector.Update(0, SelectY, Ms(5000)).ShouldBeNull();
        detector.Update(3, L3R3, Ms(3000)).ShouldBeNull();
    }

    [Fact]
    public void Tempo_zero_dispara_na_primeira_leitura()
    {
        var detector = new ComboDetector([new ComboDefinition("now", SelectY, TimeSpan.Zero)]);

        detector.Update(0, SelectY, Ms(100)).ShouldNotBeNull();
    }

    [Fact]
    public void Relogio_que_volta_nao_dispara()
    {
        var detector = Detector();
        detector.Update(0, SelectY, Ms(10_000));

        detector.Update(0, SelectY, Ms(0)).ShouldBeNull();
    }

    [Fact]
    public void Sem_combos_nunca_dispara()
    {
        var detector = new ComboDetector([]);

        detector.Update(0, SelectY, Ms(0)).ShouldBeNull();
        detector.Update(0, SelectY, Ms(60_000)).ShouldBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Indice_de_controle_fora_de_0_a_3_e_recusado(int index)
    {
        var detector = Detector();

        Should.Throw<ArgumentOutOfRangeException>(() => detector.Update(index, SelectY, Ms(0)));
        Should.Throw<ArgumentOutOfRangeException>(() => detector.Disconnect(index));
    }

    [Fact]
    public void Combos_com_os_mesmos_botoes_sao_recusados()
    {
        var twin = new ComboDefinition("twin", SelectY, TimeSpan.FromSeconds(1));

        Should.Throw<ArgumentException>(() => new ComboDetector([_closeGame, twin]));
    }

    [Fact]
    public void Id_repetido_e_recusado()
    {
        var sameId = new ComboDefinition("closeGame", L3R3, TimeSpan.FromSeconds(1));

        Should.Throw<ArgumentException>(() => new ComboDetector([_closeGame, sameId]));
    }

    [Fact]
    public void Combo_sem_botoes_ou_com_guide_e_recusado()
    {
        var empty = new ComboDefinition("empty", GamepadButtons.None, TimeSpan.FromSeconds(1));
        var guide = new ComboDefinition("guide", GamepadButtons.A | (GamepadButtons)0x0400, TimeSpan.FromSeconds(1));

        Should.Throw<ArgumentException>(() => new ComboDetector([empty]));
        Should.Throw<ArgumentException>(() => new ComboDetector([guide]));
    }

    [Fact]
    public void Tempo_negativo_e_recusado()
    {
        var negative = new ComboDefinition("negative", SelectY, TimeSpan.FromSeconds(-1));

        Should.Throw<ArgumentException>(() => new ComboDetector([negative]));
    }

    [Fact]
    public void Lista_nula_e_recusada()
    {
        Should.Throw<ArgumentNullException>(() => new ComboDetector(null!));
    }
}
