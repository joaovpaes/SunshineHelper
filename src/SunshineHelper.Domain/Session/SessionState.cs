namespace SunshineHelper.Domain.Session;

/// <summary>Where a session stood when the snapshot was last written. Every value means
/// "the environment may still be modified": the snapshot file is deleted only once a
/// revert finishes cleanly, so finding a snapshot at startup — in any state — means
/// recovery has work to do (docs/adr/0002-arquitetura-do-nucleo.md, "Recuperação após
/// queda").</summary>
public enum SessionState
{
    Applying,

    Applied,

    Reverting,

    /// <summary>Revert finished with errors, so the snapshot was kept for a later
    /// attempt instead of being deleted.</summary>
    RevertFailed,
}
