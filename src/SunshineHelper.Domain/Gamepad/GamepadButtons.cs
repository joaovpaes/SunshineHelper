namespace SunshineHelper.Domain.Gamepad;

/// <summary>
/// The digital buttons of an Xbox-style controller, with the same bit values XInput uses
/// in <c>XINPUT_GAMEPAD.wButtons</c>, so the reader can pass its mask straight in
/// (docs/adr/0015-overlay-pelo-controle.md). The Guide button (0x0400) is left out on
/// purpose: reading it needs an undocumented XInput call and it never reaches the game.
/// Triggers and sticks are analog and are not part of a combo in v1.
/// </summary>
[Flags]
public enum GamepadButtons
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,

    /// <summary>"Back" on Xbox 360 controllers, "View" on newer ones, "Select" for most
    /// players.</summary>
    Back = 0x0020,

    /// <summary>L3: pressing the left stick.</summary>
    LeftThumb = 0x0040,

    /// <summary>R3: pressing the right stick.</summary>
    RightThumb = 0x0080,

    /// <summary>LB.</summary>
    LeftShoulder = 0x0100,

    /// <summary>RB.</summary>
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>Helpers over <see cref="GamepadButtons"/> masks.</summary>
public static class GamepadButtonMask
{
    /// <summary>Every button a combo may use. Bits outside it (Guide, reserved) are
    /// dropped from a reading before the detector looks at it.</summary>
    public const GamepadButtons AllDigital =
        GamepadButtons.DPadUp | GamepadButtons.DPadDown | GamepadButtons.DPadLeft | GamepadButtons.DPadRight
        | GamepadButtons.Start | GamepadButtons.Back | GamepadButtons.LeftThumb | GamepadButtons.RightThumb
        | GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder
        | GamepadButtons.A | GamepadButtons.B | GamepadButtons.X | GamepadButtons.Y;

    /// <summary>True when <paramref name="button"/> is exactly one known button (not
    /// <see cref="GamepadButtons.None"/>, not several OR-ed together).</summary>
    public static bool IsSingleButton(GamepadButtons button)
    {
        int bits = (int)button;
        return bits != 0 && (bits & (bits - 1)) == 0 && (button & ~AllDigital) == GamepadButtons.None;
    }

    /// <summary>How many buttons are set in <paramref name="mask"/>.</summary>
    public static int Count(GamepadButtons mask) => int.PopCount((int)(mask & AllDigital));

    /// <summary>OR-s a list of buttons into one mask.</summary>
    public static GamepadButtons Combine(IEnumerable<GamepadButtons> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        var mask = GamepadButtons.None;
        foreach (var button in buttons)
        {
            mask |= button;
        }

        return mask;
    }
}
