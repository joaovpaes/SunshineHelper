using Shouldly;
using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class ComboCheckTests
{
    private const GamepadButtons SelectY = GamepadButtons.Back | GamepadButtons.Y;

    [Fact]
    public void A_good_combo_has_no_issue()
    {
        ComboCheck.Check(SelectY, 5000, GamepadButtons.None).ShouldBeEmpty();
    }

    [Fact]
    public void One_button_is_too_few()
    {
        ComboCheck.Check(GamepadButtons.A, 3000, GamepadButtons.None).ShouldBe([ComboIssue.TooFewButtons]);
    }

    [Fact]
    public void No_button_is_too_few()
    {
        ComboCheck.Check(GamepadButtons.None, 3000, GamepadButtons.None).ShouldBe([ComboIssue.TooFewButtons]);
    }

    [Fact]
    public void Five_buttons_are_too_many()
    {
        var five = GamepadButtons.A | GamepadButtons.B | GamepadButtons.X | GamepadButtons.Y | GamepadButtons.LeftShoulder;
        ComboCheck.Check(five, 3000, GamepadButtons.None).ShouldBe([ComboIssue.TooManyButtons]);
    }

    [Fact]
    public void Start_and_back_together_are_refused()
    {
        ComboCheck.Check(GamepadButtons.Start | GamepadButtons.Back, 3000, GamepadButtons.None)
            .ShouldBe([ComboIssue.StartAndBack]);
    }

    [Fact]
    public void Start_and_back_inside_a_larger_combo_are_refused_too()
    {
        ComboCheck.Check(GamepadButtons.Start | GamepadButtons.Back | GamepadButtons.A, 3000, GamepadButtons.None)
            .ShouldContain(ComboIssue.StartAndBack);
    }

    [Fact]
    public void The_same_buttons_as_the_other_combo_are_refused()
    {
        ComboCheck.Check(SelectY, 5000, SelectY).ShouldBe([ComboIssue.SameAsOther]);
    }

    [Fact]
    public void An_empty_other_combo_never_clashes()
    {
        ComboCheck.Check(SelectY, 5000, GamepadButtons.None).ShouldNotContain(ComboIssue.SameAsOther);
    }

    [Theory]
    [InlineData(499, ComboIssue.HoldTooShort)]
    [InlineData(10001, ComboIssue.HoldTooLong)]
    public void A_hold_out_of_range_is_refused(int holdMs, ComboIssue expected)
    {
        ComboCheck.Check(SelectY, holdMs, GamepadButtons.None).ShouldBe([expected]);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(10000)]
    public void The_limits_of_the_hold_are_accepted(int holdMs)
    {
        ComboCheck.Check(SelectY, holdMs, GamepadButtons.None).ShouldBeEmpty();
    }

    [Fact]
    public void Several_problems_are_all_listed()
    {
        ComboCheck.Check(GamepadButtons.A, 100, GamepadButtons.None)
            .ShouldBe([ComboIssue.TooFewButtons, ComboIssue.HoldTooShort]);
    }

    [Fact]
    public void Split_lists_the_buttons_in_the_screen_order()
    {
        ComboCheck.Split(GamepadButtons.Y | GamepadButtons.Back | GamepadButtons.DPadUp)
            .ShouldBe([GamepadButtons.Back, GamepadButtons.Y, GamepadButtons.DPadUp]);
    }

    [Fact]
    public void Split_of_nothing_is_empty()
    {
        ComboCheck.Split(GamepadButtons.None).ShouldBeEmpty();
    }
}
