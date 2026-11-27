namespace SunshineHelper.Application.CloseGame;

/// <summary>What one hold of the close-game combo did.</summary>
public enum CloseGameResult
{
    /// <summary>The close-game combo is turned off.</summary>
    Disabled,

    /// <summary>No stream is on.</summary>
    NoSession,

    /// <summary>A previous hold is still waiting for the game to close.</summary>
    Busy,

    /// <summary>Ignored: a round ended less than <see cref="CloseGameService.Cooldown"/> ago.</summary>
    CoolingDown,

    /// <summary>No window is in the foreground.</summary>
    NoTarget,

    /// <summary>The program in the foreground is protected; nothing was done.</summary>
    Protected,

    /// <summary>The launcher is in the foreground and it started more than one program with
    /// a window during the stream; the app does not guess which one is the game.</summary>
    AmbiguousTarget,

    /// <summary>The program in the foreground cannot be inspected (usually it runs as
    /// administrator); nothing was done.</summary>
    NotInspectable,

    /// <summary>Windows refused the close request (usually the game runs as administrator).</summary>
    AccessDenied,

    /// <summary>The game closed after being asked.</summary>
    Closed,

    /// <summary>The game did not close; holding the combo again within
    /// <see cref="CloseGameService.ConfirmWindow"/> ends it.</summary>
    AwaitingConfirmation,

    /// <summary>The game was ended.</summary>
    Forced,

    /// <summary>Ending the game did not work (usually it runs as administrator).</summary>
    ForceFailed,
}

/// <summary>The result of one hold, with the program it was about when there was one.</summary>
public sealed record CloseGameOutcome(
    CloseGameResult Result,
    string? ProcessName = null,
    CloseGameProtectionReason Protection = CloseGameProtectionReason.None);
