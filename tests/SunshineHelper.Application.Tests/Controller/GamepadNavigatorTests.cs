using Shouldly;
using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class GamepadNavigatorTests
{
    private static TimeSpan At(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void A_button_counts_once_however_long_it_is_held()
    {
        var navigator = new GamepadNavigator();

        navigator.Update(GamepadButtons.A, At(0)).ShouldBe([NavInput.Confirm]);
        navigator.Update(GamepadButtons.A, At(16)).ShouldBeEmpty();
        navigator.Update(GamepadButtons.A, At(5000)).ShouldBeEmpty();
    }

    [Fact]
    public void Letting_go_and_pressing_again_counts_again()
    {
        var navigator = new GamepadNavigator();
        navigator.Update(GamepadButtons.B, At(0));
        navigator.Update(GamepadButtons.None, At(16));

        navigator.Update(GamepadButtons.B, At(32)).ShouldBe([NavInput.Back]);
    }

    [Theory]
    [InlineData(GamepadButtons.X, NavInput.X)]
    [InlineData(GamepadButtons.Y, NavInput.Y)]
    [InlineData(GamepadButtons.DPadLeft, NavInput.Left)]
    [InlineData(GamepadButtons.DPadRight, NavInput.Right)]
    public void Each_button_maps_to_its_press(GamepadButtons button, NavInput expected)
    {
        new GamepadNavigator().Update(button, At(0)).ShouldBe([expected]);
    }

    [Fact]
    public void A_direction_repeats_after_the_delay_and_then_at_the_interval()
    {
        var navigator = new GamepadNavigator();

        navigator.Update(GamepadButtons.DPadDown, At(0)).ShouldBe([NavInput.Down]);
        navigator.Update(GamepadButtons.DPadDown, At(399)).ShouldBeEmpty();
        navigator.Update(GamepadButtons.DPadDown, At(400)).ShouldBe([NavInput.Down]);
        navigator.Update(GamepadButtons.DPadDown, At(519)).ShouldBeEmpty();
        navigator.Update(GamepadButtons.DPadDown, At(520)).ShouldBe([NavInput.Down]);
    }

    [Fact]
    public void Letting_go_of_a_direction_stops_the_repeat_and_restarts_the_delay()
    {
        var navigator = new GamepadNavigator();
        navigator.Update(GamepadButtons.DPadUp, At(0));
        navigator.Update(GamepadButtons.None, At(100));

        navigator.Update(GamepadButtons.DPadUp, At(200)).ShouldBe([NavInput.Up]);
        navigator.Update(GamepadButtons.DPadUp, At(500)).ShouldBeEmpty();
    }

    [Fact]
    public void The_buttons_held_at_reset_are_not_presses()
    {
        var navigator = new GamepadNavigator();
        navigator.Reset(GamepadButtons.A | GamepadButtons.LeftThumb);

        navigator.Update(GamepadButtons.A | GamepadButtons.LeftThumb, At(0)).ShouldBeEmpty();
        navigator.Update(GamepadButtons.None, At(16)).ShouldBeEmpty();
        navigator.Update(GamepadButtons.A, At(32)).ShouldBe([NavInput.Confirm]);
    }

    [Fact]
    public void Buttons_that_do_not_navigate_are_ignored()
    {
        new GamepadNavigator().Update(GamepadButtons.Start | GamepadButtons.LeftShoulder | GamepadButtons.RightThumb, At(0))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Two_buttons_pressed_together_both_count()
    {
        new GamepadNavigator().Update(GamepadButtons.DPadUp | GamepadButtons.A, At(0))
            .ShouldBe([NavInput.Up, NavInput.Confirm]);
    }
}
