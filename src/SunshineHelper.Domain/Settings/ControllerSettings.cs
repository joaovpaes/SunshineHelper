using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Domain.Settings;

/// <summary>
/// What the controller does during a stream (docs/adr/0015-overlay-pelo-controle.md and
/// docs/adr/0016-fechar-jogo-pelo-controle.md). Optional in settings.json: older files load
/// with these defaults, so the schema version does not change.
/// </summary>
public sealed record ControllerSettings
{
    /// <summary>Id the detector reports when the close-game combo fires.</summary>
    public const string CloseGameComboId = "closeGame";

    /// <summary>Id the detector reports when the overlay combo fires.</summary>
    public const string OverlayComboId = "overlay";

    public CloseGameSettings CloseGame { get; init; } = new();

    public OverlaySettings Overlay { get; init; } = new();

    /// <summary>The combos the detector should watch right now: the close-game one only
    /// when the user turned it on. The overlay combo joins once the overlay window exists
    /// (ADR 0015, PR F).</summary>
    public IReadOnlyList<ComboDefinition> ActiveCombos()
    {
        var combos = new List<ComboDefinition>();
        if (CloseGame.Enabled)
        {
            combos.Add(CloseGame.Combo.ToDefinition(CloseGameComboId));
        }

        return combos;
    }
}

/// <summary>A combo as stored in settings.json: the buttons (by name) and how long to hold
/// them.</summary>
public sealed record ComboSettings
{
    /// <summary>Fewest buttons a combo may have.</summary>
    public const int MinButtons = 2;

    /// <summary>Most buttons a combo may have.</summary>
    public const int MaxButtons = 4;

    /// <summary>Shortest hold accepted.</summary>
    public const int MinHoldMs = 500;

    /// <summary>Longest hold accepted.</summary>
    public const int MaxHoldMs = 10_000;

    public IReadOnlyList<GamepadButtons> Buttons { get; init; } = [];

    public int HoldMs { get; init; }

    /// <summary>The buttons as one mask. A method, not a property, so it is not written to
    /// settings.json.</summary>
    public GamepadButtons ToMask() => GamepadButtonMask.Combine(Buttons);

    public ComboDefinition ToDefinition(string id) => new(id, ToMask(), TimeSpan.FromMilliseconds(HoldMs));
}

/// <summary>"Close the focused game" by holding a combo (ADR 0016). Off by default: closing
/// a program is destructive, and someone who just downloaded the app should not have it on
/// without knowing.</summary>
public sealed record CloseGameSettings
{
    /// <summary>Shortest wait for the game to close after being asked.</summary>
    public const int MinPoliteTimeoutMs = 1_000;

    /// <summary>Longest wait for the game to close after being asked.</summary>
    public const int MaxPoliteTimeoutMs = 30_000;

    public bool Enabled { get; init; }

    /// <summary>Select (Back) + Y held for 5 s, the author's choice (ADR 0015).</summary>
    public ComboSettings Combo { get; init; } = new()
    {
        Buttons = [GamepadButtons.Back, GamepadButtons.Y],
        HoldMs = 5_000,
    };

    /// <summary>How long the game gets to close after the polite request (like clicking
    /// its X) before the app offers to end it.</summary>
    public int PoliteTimeoutMs { get; init; } = 5_000;

    /// <summary>End the game as soon as <see cref="PoliteTimeoutMs"/> runs out, without
    /// waiting for a second hold of the combo. Off by default.</summary>
    public bool ForceWithoutConfirm { get; init; }

    /// <summary>After the game is gone, try to bring the launcher's window to the front.</summary>
    public bool ReturnToLauncher { get; init; } = true;

    /// <summary>Extra process names the combo must never close, on top of the fixed list
    /// and the launcher list. Same format as <see cref="LauncherSettings.ProcessNames"/>.</summary>
    public IReadOnlyList<string> ProtectedNames { get; init; } = [];
}

/// <summary>The overlay opened from the controller (ADR 0015).</summary>
public sealed record OverlaySettings
{
    /// <summary>L3 + R3 held for 3 s: rarely pressed together on purpose, and opening the
    /// overlay destroys nothing, so it can be shorter than the close-game hold.</summary>
    public ComboSettings Combo { get; init; } = new()
    {
        Buttons = [GamepadButtons.LeftThumb, GamepadButtons.RightThumb],
        HoldMs = 3_000,
    };
}
