using Shouldly;
using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class GamepadPollerTests
{
    private readonly ScriptedGamepadReader _reader = new();
    private readonly GamepadPoller _poller;

    public GamepadPollerTests()
    {
        _poller = new GamepadPoller(_reader);
    }

    private static TimeSpan At(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void With_no_controller_nothing_is_held_and_none_is_connected()
    {
        _poller.Read(At(0)).ShouldBe(GamepadButtons.None);

        _poller.AnyConnected.ShouldBeFalse();
    }

    [Fact]
    public void The_buttons_of_every_connected_controller_are_joined()
    {
        _reader.Connect(0, GamepadButtons.A);
        _reader.Connect(2, GamepadButtons.Y);

        _poller.Read(At(0)).ShouldBe(GamepadButtons.A | GamepadButtons.Y);

        _poller.AnyConnected.ShouldBeTrue();
    }

    [Fact]
    public void An_empty_slot_is_not_asked_again_before_a_second()
    {
        _poller.Read(At(0));
        int readsAfterFirst = _reader.Reads;

        _poller.Read(At(500));

        _reader.Reads.ShouldBe(readsAfterFirst);
    }

    [Fact]
    public void An_empty_slot_is_asked_again_after_a_second_and_a_new_controller_is_found()
    {
        _poller.Read(At(0));
        _reader.Connect(1, GamepadButtons.B);

        _poller.Read(At(1000)).ShouldBe(GamepadButtons.B);

        _poller.AnyConnected.ShouldBeTrue();
    }

    [Fact]
    public void A_connected_controller_is_read_on_every_call()
    {
        _reader.Connect(0, GamepadButtons.A);
        _poller.Read(At(0));

        _reader.Connect(0, GamepadButtons.X);

        _poller.Read(At(16)).ShouldBe(GamepadButtons.X);
    }

    [Fact]
    public void A_controller_that_goes_away_stops_counting()
    {
        _reader.Connect(0, GamepadButtons.A);
        _poller.Read(At(0));
        _reader.Disconnect(0);

        _poller.Read(At(16)).ShouldBe(GamepadButtons.None);

        _poller.AnyConnected.ShouldBeFalse();
    }

    [Fact]
    public void A_missing_reader_is_refused()
    {
        Should.Throw<ArgumentNullException>(() => new GamepadPoller(null!));
    }
}
