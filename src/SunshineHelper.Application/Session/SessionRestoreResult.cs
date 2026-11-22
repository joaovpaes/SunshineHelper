namespace SunshineHelper.Application.Session;

public enum RestoreStatus
{
    /// <summary>No snapshot on disk: no session to put back.</summary>
    NothingToDo,

    Restored,

    /// <summary>The environment is back as far as this version of the app can take it,
    /// but something in the snapshot could not be handled — a module that no longer
    /// exists, for instance. The snapshot was still removed: keeping it would block
    /// every future session.</summary>
    Incomplete,

    /// <summary>Something could not be put back. The snapshot was kept so a later
    /// attempt can try again.</summary>
    Failed,
}

/// <summary>The outcome of putting the environment back, whether at the end of a session
/// or when recovering a snapshot left behind by a crash or a reboot.</summary>
public sealed record SessionRestoreResult
{
    public required RestoreStatus Status { get; init; }

    /// <summary>True when the PC restarted since the snapshot was written, in which case
    /// only the modules whose state survives a reboot were touched.</summary>
    public bool RebootDetected { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];
}
