using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Application.Controller;

/// <summary>What the controller asked the overlay to do, in the Xbox-style navigation the
/// author chose (docs/adr/0015-overlay-pelo-controle.md): A confirms, B goes back.</summary>
public enum NavInput
{
    Up,
    Down,
    Left,
    Right,

    /// <summary>The A button.</summary>
    Confirm,

    /// <summary>The B button.</summary>
    Back,

    /// <summary>The X button, a secondary action of the screen.</summary>
    X,

    /// <summary>The Y button, a secondary action of the screen.</summary>
    Y,
}

/// <summary>
/// Turns "which buttons are held now" into navigation presses: each button press counts once
/// (not for as long as it is held), and the D-pad repeats while it is held, like a menu on a
/// console. Pure: the overlay feeds it a reading and a time on each tick.
/// </summary>
public sealed class GamepadNavigator
{
    /// <summary>How long a D-pad direction is held before it starts to repeat.</summary>
    public static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>The time between repeats while a direction stays held.</summary>
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(120);

    private static readonly (GamepadButtons Button, NavInput Input)[] _directions =
    [
        (GamepadButtons.DPadUp, NavInput.Up),
        (GamepadButtons.DPadDown, NavInput.Down),
        (GamepadButtons.DPadLeft, NavInput.Left),
        (GamepadButtons.DPadRight, NavInput.Right),
    ];

    private static readonly (GamepadButtons Button, NavInput Input)[] _actions =
    [
        (GamepadButtons.A, NavInput.Confirm),
        (GamepadButtons.B, NavInput.Back),
        (GamepadButtons.X, NavInput.X),
        (GamepadButtons.Y, NavInput.Y),
    ];

    private readonly TimeSpan[] _nextRepeat = new TimeSpan[_directions.Length];
    private GamepadButtons _previous;

    /// <summary>Takes what is held right now as already pressed, so the buttons still down
    /// when the overlay opens (the combo that opened it) do not count as presses.</summary>
    public void Reset(GamepadButtons held)
    {
        _previous = held & GamepadButtonMask.AllDigital;
    }

    /// <summary>The presses since the last call, in the order they happened to be checked
    /// (directions first, then A, B, X, Y).</summary>
    public IReadOnlyList<NavInput> Update(GamepadButtons held, TimeSpan now)
    {
        held &= GamepadButtonMask.AllDigital;
        var presses = new List<NavInput>();

        for (int i = 0; i < _directions.Length; i++)
        {
            var (button, input) = _directions[i];
            bool down = (held & button) != GamepadButtons.None;
            bool wasDown = (_previous & button) != GamepadButtons.None;
            if (!down)
            {
                continue;
            }

            if (!wasDown)
            {
                presses.Add(input);
                _nextRepeat[i] = now + RepeatDelay;
            }
            else if (now >= _nextRepeat[i])
            {
                presses.Add(input);
                _nextRepeat[i] = now + RepeatInterval;
            }
        }

        foreach (var (button, input) in _actions)
        {
            if ((held & button) != GamepadButtons.None && (_previous & button) == GamepadButtons.None)
            {
                presses.Add(input);
            }
        }

        _previous = held;
        return presses;
    }
}
