using Shouldly;
using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Infrastructure.Controller;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Controller;

public class XInputGamepadReaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Ler_um_slot_nunca_lanca_mesmo_sem_controle(int index)
    {
        var reader = new XInputGamepadReader();

        var sample = reader.Read(index);

        // Na máquina do CI não há controle; num PC com controle também vale: só botões conhecidos.
        (sample.Buttons & ~GamepadButtonMask.AllDigital).ShouldBe(GamepadButtons.None);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void Slot_fora_de_0_a_3_vira_nao_conectado(int index)
    {
        var sample = new XInputGamepadReader().Read(index);

        sample.Connected.ShouldBeFalse();
        sample.Buttons.ShouldBe(GamepadButtons.None);
    }
}
