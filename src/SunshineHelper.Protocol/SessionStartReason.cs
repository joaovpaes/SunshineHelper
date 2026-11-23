namespace SunshineHelper.Protocol;

/// <summary>Why a <see cref="TriggerOp.SessionStart"/> was sent. Absent (null) means
/// Sunshine's Prep Command, through the trigger auxiliary: a brand-new launch.</summary>
public static class SessionStartReason
{
    /// <summary>The client came back with <c>/resume</c> after a disconnect that reverted
    /// the screen. Only sent in-process, by the log reader. Unlike a new launch, it does
    /// not prove that a session still on disk is dead, so it never restores one first
    /// (docs/adr/0011-falha-parcial-e-sessao-presa.md).</summary>
    public const string Resume = "resume";
}
