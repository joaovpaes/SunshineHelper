using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Application.Controller;

/// <summary>
/// "Press the buttons..." on the Controller screen: the user holds the buttons of the new
/// combo and lets go, and the combo is every button seen while they were held. Pure: the
/// screen feeds it the buttons of the controller on each tick.
/// </summary>
public sealed class ComboRecorder
{
    private GamepadButtons _seen;
    private bool _pressing;

    /// <summary>True once something was pressed and then everything was let go.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The buttons seen so far (the whole combo once <see cref="IsFinished"/>).</summary>
    public GamepadButtons Recorded => _seen;

    /// <summary>Starts over, for a new recording.</summary>
    public void Reset()
    {
        _seen = GamepadButtons.None;
        _pressing = false;
        IsFinished = false;
    }

    /// <summary>Takes the buttons held right now. Once finished, later readings are ignored
    /// until <see cref="Reset"/>.</summary>
    public void Feed(GamepadButtons buttons)
    {
        if (IsFinished)
        {
            return;
        }

        buttons &= GamepadButtonMask.AllDigital;
        if (buttons != GamepadButtons.None)
        {
            _seen |= buttons;
            _pressing = true;
        }
        else if (_pressing)
        {
            IsFinished = true;
        }
    }
}

/// <summary>
/// The hold bar of the Controller screen: how far along the user is in holding exactly the
/// buttons of a combo, so they can try the time before relying on it. Same rule as the real
/// detector: only the exact buttons count, one more or one less starts over.
/// </summary>
public sealed class ComboHoldMeter
{
    private readonly GamepadButtons _combo;
    private readonly TimeSpan _hold;
    private TimeSpan? _since;

    public ComboHoldMeter(GamepadButtons combo, TimeSpan hold)
    {
        if (hold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(hold), "The hold time must be positive.");
        }

        _combo = combo;
        _hold = hold;
    }

    /// <summary>True once the combo was held for the whole time (until it is let go).</summary>
    public bool Reached { get; private set; }

    /// <summary>Takes the buttons held at <paramref name="now"/> and returns how much of the
    /// time has been held, from 0 to 1.</summary>
    public double Update(GamepadButtons buttons, TimeSpan now)
    {
        buttons &= GamepadButtonMask.AllDigital;
        if (_combo == GamepadButtons.None || buttons != _combo)
        {
            _since = null;
            Reached = false;
            return 0;
        }

        _since ??= now;
        double progress = (now - _since.Value) / _hold;
        progress = Math.Clamp(progress, 0, 1);
        Reached = progress >= 1;
        return progress;
    }
}
