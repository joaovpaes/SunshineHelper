using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Application.Controller;

/// <summary>
/// One look at all the controller slots, for the screens that read the controller on their own
/// (the Controller screen and the overlay): the buttons held on every controller together. A
/// slot with nothing plugged in is looked at again only once a second, because asking XInput
/// about an empty slot is slow on some systems and nobody plugs a controller in at 60 Hz.
/// </summary>
public sealed class GamepadPoller
{
    /// <summary>How long an empty slot is left alone before it is looked at again.</summary>
    public static readonly TimeSpan EmptySlotInterval = TimeSpan.FromSeconds(1);

    private readonly IGamepadReader _reader;
    private readonly bool[] _connected = new bool[ComboDetector.MaxControllers];
    private readonly TimeSpan[] _nextLookup = new TimeSpan[ComboDetector.MaxControllers];

    public GamepadPoller(IGamepadReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>True when the last readings found at least one controller.</summary>
    public bool AnyConnected => Array.Exists(_connected, connected => connected);

    /// <summary>The buttons held on every connected controller, OR-ed together.</summary>
    /// <param name="now">Any clock that only moves forward.</param>
    public GamepadButtons Read(TimeSpan now)
    {
        var held = GamepadButtons.None;
        for (int slot = 0; slot < ComboDetector.MaxControllers; slot++)
        {
            if (!_connected[slot] && now < _nextLookup[slot])
            {
                continue;
            }

            var sample = _reader.Read(slot);
            _connected[slot] = sample.Connected;
            if (sample.Connected)
            {
                held |= sample.Buttons;
            }
            else
            {
                _nextLookup[slot] = now + EmptySlotInterval;
            }
        }

        return held;
    }
}
