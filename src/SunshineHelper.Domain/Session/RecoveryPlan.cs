namespace SunshineHelper.Domain.Session;

/// <summary>What <see cref="RecoveryDecider"/> decided to do with a snapshot found on
/// disk: which modules to revert, in which order, and which ones are deliberately left
/// alone — the second list exists so the app can tell the user <em>why</em> something was
/// not restored.</summary>
public sealed record RecoveryPlan
{
    /// <summary>True when the PC restarted since the snapshot was written.</summary>
    public required bool RebootDetected { get; init; }

    /// <summary>The state the session was left in, for diagnostics.</summary>
    public required SessionState State { get; init; }

    /// <summary>Modules to revert, already in revert order (the reverse of apply order).</summary>
    public required IReadOnlyList<CapturedModule> ModulesToRevert { get; init; }

    public required IReadOnlyList<CapturedModule> ModulesLeftAlone { get; init; }

    /// <summary>Nothing to restore: the caller can discard the snapshot.</summary>
    public bool IsEmpty => ModulesToRevert.Count == 0;
}
