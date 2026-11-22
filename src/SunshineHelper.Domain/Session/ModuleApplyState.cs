namespace SunshineHelper.Domain.Session;

/// <summary>How far one module got during a session. The orchestrator rewrites the
/// snapshot on disk every time this changes, which is what makes recovery after a crash
/// precise: without it, a crash mid-apply leaves no way to tell which modules actually
/// touched the environment, and reverting a module that was never applied would change
/// something the profile said to leave alone.</summary>
public enum ModuleApplyState
{
    /// <summary>State captured, nothing applied yet.</summary>
    Pending,

    /// <summary>Apply started and its outcome is unknown. This is the state a crash
    /// leaves behind, and it must be reverted: the module may be half-applied.</summary>
    Applying,

    Applied,

    /// <summary>The profile asked for no change in this module, so nothing was applied
    /// and nothing must be reverted.</summary>
    Skipped,

    /// <summary>Apply failed. Reverted anyway: a module that failed partway through is
    /// the most likely one to be half-applied.</summary>
    Failed,
}
