using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Controller;

/// <summary>What is wrong with a combo the user is setting up.</summary>
public enum ComboIssue
{
    /// <summary>Fewer than <see cref="ComboSettings.MinButtons"/> buttons.</summary>
    TooFewButtons,

    /// <summary>More than <see cref="ComboSettings.MaxButtons"/> buttons.</summary>
    TooManyButtons,

    /// <summary>Start and Back together: Start + Select, and Moonlight's quit shortcut.</summary>
    StartAndBack,

    /// <summary>The same buttons as the other combo.</summary>
    SameAsOther,

    /// <summary>Hold shorter than <see cref="ComboSettings.MinHoldMs"/>.</summary>
    HoldTooShort,

    /// <summary>Hold longer than <see cref="ComboSettings.MaxHoldMs"/>.</summary>
    HoldTooLong,
}

/// <summary>
/// The rules every combo follows (docs/adr/0015-overlay-pelo-controle.md), as codes the
/// Controller screen turns into text in the language of the interface. Saving still goes
/// through <c>SettingsValidator</c>, which has the same rules: this only lets the screen warn
/// before the user tries to save.
/// </summary>
public static class ComboCheck
{
    private static readonly GamepadButtons[] _displayOrder =
    [
        GamepadButtons.Back,
        GamepadButtons.Start,
        GamepadButtons.A,
        GamepadButtons.B,
        GamepadButtons.X,
        GamepadButtons.Y,
        GamepadButtons.LeftShoulder,
        GamepadButtons.RightShoulder,
        GamepadButtons.LeftThumb,
        GamepadButtons.RightThumb,
        GamepadButtons.DPadUp,
        GamepadButtons.DPadDown,
        GamepadButtons.DPadLeft,
        GamepadButtons.DPadRight,
    ];

    /// <param name="buttons">The combo's buttons.</param>
    /// <param name="holdMs">How long it is held, in milliseconds.</param>
    /// <param name="otherCombo">The buttons of the other combo, or
    /// <see cref="GamepadButtons.None"/> when there is nothing to compare with.</param>
    /// <returns>Every problem found; empty when the combo can be saved.</returns>
    public static IReadOnlyList<ComboIssue> Check(GamepadButtons buttons, int holdMs, GamepadButtons otherCombo)
    {
        var issues = new List<ComboIssue>();
        int count = GamepadButtonMask.Count(buttons);
        if (count < ComboSettings.MinButtons)
        {
            issues.Add(ComboIssue.TooFewButtons);
        }
        else if (count > ComboSettings.MaxButtons)
        {
            issues.Add(ComboIssue.TooManyButtons);
        }

        const GamepadButtons startAndBack = GamepadButtons.Start | GamepadButtons.Back;
        if ((buttons & startAndBack) == startAndBack)
        {
            issues.Add(ComboIssue.StartAndBack);
        }

        if (otherCombo != GamepadButtons.None && buttons == otherCombo)
        {
            issues.Add(ComboIssue.SameAsOther);
        }

        if (holdMs < ComboSettings.MinHoldMs)
        {
            issues.Add(ComboIssue.HoldTooShort);
        }
        else if (holdMs > ComboSettings.MaxHoldMs)
        {
            issues.Add(ComboIssue.HoldTooLong);
        }

        return issues;
    }

    /// <summary>The buttons of <paramref name="mask"/>, one by one, in a fixed order
    /// (the order the screen lists them in).</summary>
    public static IReadOnlyList<GamepadButtons> Split(GamepadButtons mask)
    {
        var result = new List<GamepadButtons>();
        foreach (var button in _displayOrder)
        {
            if ((mask & button) != GamepadButtons.None)
            {
                result.Add(button);
            }
        }

        return result;
    }
}
