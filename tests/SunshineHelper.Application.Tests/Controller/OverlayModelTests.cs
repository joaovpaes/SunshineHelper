using Shouldly;
using SunshineHelper.Application.Controller;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class OverlayModelTests
{
    [Fact]
    public void It_starts_on_the_only_entry_that_works()
    {
        var model = new OverlayModel();

        model.Selected.ShouldBe(OverlayItem.Restore);
        model.Items.Count.ShouldBe(5);
        model.IsAvailable(OverlayItem.Profiles).ShouldBeFalse();
        model.IsAvailable(OverlayItem.Restore).ShouldBeTrue();
    }

    [Theory]
    [InlineData(NavInput.Up)]
    [InlineData(NavInput.Down)]
    public void Moving_never_lands_on_an_entry_that_is_not_ready(NavInput input)
    {
        var model = new OverlayModel();

        model.Handle(input).ShouldBe(OverlayRequest.None);

        model.Selected.ShouldBe(OverlayItem.Restore);
    }

    [Fact]
    public void B_on_the_home_screen_closes_the_overlay()
    {
        new OverlayModel().Handle(NavInput.Back).ShouldBe(OverlayRequest.Close);
    }

    [Fact]
    public void A_on_restore_asks_first_and_does_not_restore_yet()
    {
        var model = new OverlayModel();

        model.Handle(NavInput.Confirm).ShouldBe(OverlayRequest.None);

        model.Confirming.ShouldBeTrue();
    }

    [Fact]
    public void A_on_the_question_restores()
    {
        var model = new OverlayModel();
        model.Handle(NavInput.Confirm);

        model.Handle(NavInput.Confirm).ShouldBe(OverlayRequest.RestoreNow);

        model.Confirming.ShouldBeFalse();
    }

    [Fact]
    public void B_on_the_question_cancels_and_does_not_close_the_overlay()
    {
        var model = new OverlayModel();
        model.Handle(NavInput.Confirm);

        model.Handle(NavInput.Back).ShouldBe(OverlayRequest.None);

        model.Confirming.ShouldBeFalse();
        model.Handle(NavInput.Back).ShouldBe(OverlayRequest.Close);
    }

    [Theory]
    [InlineData(NavInput.Up)]
    [InlineData(NavInput.Down)]
    [InlineData(NavInput.Left)]
    [InlineData(NavInput.X)]
    [InlineData(NavInput.Y)]
    public void Other_presses_do_nothing_while_the_question_is_open(NavInput input)
    {
        var model = new OverlayModel();
        model.Handle(NavInput.Confirm);

        model.Handle(input).ShouldBe(OverlayRequest.None);

        model.Confirming.ShouldBeTrue();
    }

    [Theory]
    [InlineData(NavInput.Left)]
    [InlineData(NavInput.Right)]
    [InlineData(NavInput.X)]
    [InlineData(NavInput.Y)]
    public void Presses_with_no_meaning_on_the_home_screen_do_nothing(NavInput input)
    {
        var model = new OverlayModel();

        model.Handle(input).ShouldBe(OverlayRequest.None);

        model.Confirming.ShouldBeFalse();
    }

    [Fact]
    public void After_a_restore_nothing_is_left_to_select_or_confirm()
    {
        var model = new OverlayModel();
        model.Handle(NavInput.Confirm);
        model.Handle(NavInput.Confirm);

        model.MarkRestored();

        model.Restored.ShouldBeTrue();
        model.Selected.ShouldBeNull();
        model.IsAvailable(OverlayItem.Restore).ShouldBeFalse();
        model.Handle(NavInput.Confirm).ShouldBe(OverlayRequest.None);
        model.Handle(NavInput.Down).ShouldBe(OverlayRequest.None);
        model.Handle(NavInput.Back).ShouldBe(OverlayRequest.Close);
    }
}
