using System.Text.Json.Serialization;

namespace SunshineHelper.Domain.Session;

/// <summary>One module's entry in the session snapshot: what the core knows about it
/// (identity, ordering, whether its state outlives a reboot, how far it got) plus the
/// module's own opaque <see cref="ModuleSnapshot"/>.</summary>
public sealed record CapturedModule
{
    public required string ModuleId { get; init; }

    /// <summary>Apply order. Revert runs in the reverse order.</summary>
    public required int Order { get; init; }

    /// <summary>True when this module's state survives a restart of the PC — the RTSS
    /// profile, for instance, is a file on disk. Windows restores resolution and HDR by
    /// itself, so those modules set this to false and are left alone after a reboot
    /// (docs/adr/0002-arquitetura-do-nucleo.md, "Recuperação após queda").</summary>
    public required bool SurvivesReboot { get; init; }

    public required ModuleApplyState ApplyState { get; init; }

    public required ModuleSnapshot Snapshot { get; init; }

    /// <summary>True when this module may have changed the environment and therefore has
    /// to be reverted. <see cref="ModuleApplyState.Skipped"/> and
    /// <see cref="ModuleApplyState.Pending"/> never changed anything, and reverting them
    /// would move a value the profile said to leave alone.</summary>
    [JsonIgnore]
    public bool NeedsRevert =>
        ApplyState is ModuleApplyState.Applying or ModuleApplyState.Applied or ModuleApplyState.Failed;
}
