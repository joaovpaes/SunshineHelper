using Shouldly;
using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class ComboRecorderTests
{
    private const GamepadButtons SelectY = GamepadButtons.Back | GamepadButtons.Y;

    [Fact]
    public void Nothing_pressed_keeps_waiting()
    {
        var recorder = new ComboRecorder();

        recorder.Feed(GamepadButtons.None);
        recorder.Feed(GamepadButtons.None);

        recorder.IsFinished.ShouldBeFalse();
        recorder.Recorded.ShouldBe(GamepadButtons.None);
    }

    [Fact]
    public void Pressing_one_button_then_the_other_and_letting_go_records_both()
    {
        var recorder = new ComboRecorder();

        recorder.Feed(GamepadButtons.Back);
        recorder.Feed(SelectY);
        recorder.IsFinished.ShouldBeFalse();
        recorder.Feed(GamepadButtons.None);

        recorder.IsFinished.ShouldBeTrue();
        recorder.Recorded.ShouldBe(SelectY);
    }

    [Fact]
    public void Letting_one_button_go_early_does_not_finish_while_another_is_held()
    {
        var recorder = new ComboRecorder();

        recorder.Feed(SelectY);
        recorder.Feed(GamepadButtons.Y);

        recorder.IsFinished.ShouldBeFalse();
        recorder.Recorded.ShouldBe(SelectY);
    }

    [Fact]
    public void Readings_after_it_finished_are_ignored()
    {
        var recorder = new ComboRecorder();
        recorder.Feed(SelectY);
        recorder.Feed(GamepadButtons.None);

        recorder.Feed(GamepadButtons.A);

        recorder.Recorded.ShouldBe(SelectY);
    }

    [Fact]
    public void Reset_starts_a_new_recording()
    {
        var recorder = new ComboRecorder();
        recorder.Feed(SelectY);
        recorder.Feed(GamepadButtons.None);

        recorder.Reset();
        recorder.Feed(GamepadButtons.A | GamepadButtons.B);
        recorder.Feed(GamepadButtons.None);

        recorder.Recorded.ShouldBe(GamepadButtons.A | GamepadButtons.B);
    }

    [Fact]
    public void Bits_outside_the_known_buttons_are_dropped()
    {
        var recorder = new ComboRecorder();

        recorder.Feed(GamepadButtons.A | (GamepadButtons)0x0400);
        recorder.Feed(GamepadButtons.None);

        recorder.Recorded.ShouldBe(GamepadButtons.A);
    }
}

public class ComboHoldMeterTests
{
    private const GamepadButtons SelectY = GamepadButtons.Back | GamepadButtons.Y;
    private static readonly TimeSpan _hold = TimeSpan.FromSeconds(4);

    [Fact]
    public void Holding_the_exact_buttons_fills_the_bar_with_time()
    {
        var meter = new ComboHoldMeter(SelectY, _hold);

        meter.Update(SelectY, TimeSpan.FromSeconds(10)).ShouldBe(0);
        meter.Update(SelectY, TimeSpan.FromSeconds(12)).ShouldBe(0.5);
        meter.Reached.ShouldBeFalse();
    }

    [Fact]
    public void Holding_it_for_the_whole_time_reaches_it_and_stays_full()
    {
        var meter = new ComboHoldMeter(SelectY, _hold);
        meter.Update(SelectY, TimeSpan.Zero);

        meter.Update(SelectY, TimeSpan.FromSeconds(4)).ShouldBe(1);
        meter.Reached.ShouldBeTrue();
        meter.Update(SelectY, TimeSpan.FromSeconds(9)).ShouldBe(1);
    }

    [Fact]
    public void Letting_go_starts_over()
    {
        var meter = new ComboHoldMeter(SelectY, _hold);
        meter.Update(SelectY, TimeSpan.Zero);
        meter.Update(SelectY, TimeSpan.FromSeconds(3));

        meter.Update(GamepadButtons.None, TimeSpan.FromMilliseconds(3100)).ShouldBe(0);
        meter.Update(SelectY, TimeSpan.FromMilliseconds(3200)).ShouldBe(0);
        meter.Update(SelectY, TimeSpan.FromMilliseconds(5200)).ShouldBe(0.5);
    }

    [Fact]
    public void One_button_more_starts_over_like_the_real_detector()
    {
        var meter = new ComboHoldMeter(SelectY, _hold);
        meter.Update(SelectY, TimeSpan.Zero);

        meter.Update(SelectY | GamepadButtons.A, TimeSpan.FromSeconds(2)).ShouldBe(0);
        meter.Reached.ShouldBeFalse();
    }

    [Fact]
    public void An_empty_combo_never_fills()
    {
        var meter = new ComboHoldMeter(GamepadButtons.None, _hold);

        meter.Update(GamepadButtons.None, TimeSpan.FromSeconds(9)).ShouldBe(0);
        meter.Reached.ShouldBeFalse();
    }

    [Fact]
    public void A_hold_that_is_not_positive_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ComboHoldMeter(SelectY, TimeSpan.Zero));
    }
}
