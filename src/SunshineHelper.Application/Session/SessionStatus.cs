namespace SunshineHelper.Application.Session;

public enum SessionStatusKind
{
    /// <summary>No snapshot on disk: the environment is the user's own.</summary>
    Idle,

    /// <summary>A session is being applied, is applied, or is being reverted.</summary>
    Active,

    /// <summary>A revert failed and left its snapshot behind, or the snapshot cannot be
    /// read: the environment may still be modified and the user should know.</summary>
    NeedsAttention,
}

/// <summary>What the Dashboard shows about the session, read from the snapshot on disk.</summary>
public sealed record SessionStatus(
    SessionStatusKind Kind,
    string? ClientName = null,
    string? ProfileId = null,
    DateTimeOffset? Since = null)
{
    /// <summary>Ids of the modules that failed to apply and were put back on their own:
    /// the session is running without them (docs/adr/0011).</summary>
    public IReadOnlyList<string> NotAppliedModules { get; init; } = [];
}
