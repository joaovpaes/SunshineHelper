using Shouldly;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Domain.Tests.Gamepad;

public class GamepadButtonMaskTests
{
    private static readonly GamepadButtons[] _selectAndY = [GamepadButtons.Back, GamepadButtons.Y];

    [Theory]
    [InlineData(GamepadButtons.A, true)]
    [InlineData(GamepadButtons.Back, true)]
    [InlineData(GamepadButtons.DPadUp, true)]
    [InlineData(GamepadButtons.None, false)]
    [InlineData(GamepadButtons.A | GamepadButtons.B, false)]
    [InlineData((GamepadButtons)0x0400, false)]
    [InlineData((GamepadButtons)0x0800, false)]
    public void IsSingleButton_aceita_so_um_botao_conhecido(GamepadButtons button, bool expected)
    {
        GamepadButtonMask.IsSingleButton(button).ShouldBe(expected);
    }

    [Fact]
    public void Count_conta_so_botoes_conhecidos()
    {
        GamepadButtonMask.Count(GamepadButtons.Back | GamepadButtons.Y | (GamepadButtons)0x0400).ShouldBe(2);
        GamepadButtonMask.Count(GamepadButtons.None).ShouldBe(0);
    }

    [Fact]
    public void Combine_junta_os_botoes()
    {
        GamepadButtonMask.Combine(_selectAndY).ShouldBe(GamepadButtons.Back | GamepadButtons.Y);
        GamepadButtonMask.Combine([]).ShouldBe(GamepadButtons.None);
    }

    [Fact]
    public void Combine_lista_nula_e_recusada()
    {
        Should.Throw<ArgumentNullException>(() => GamepadButtonMask.Combine(null!));
    }
}
