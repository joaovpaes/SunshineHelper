using System.Threading;

namespace SunshineHelper.Application.Session;

/// <summary>
/// Where the session snapshot lives. The store owns its own path — one snapshot per
/// machine, at <c>%LOCALAPPDATA%\SunshineHelper\state\current-session.json</c> — so
/// callers cannot write it somewhere the recovery path will not look.
/// </summary>
/// <remarks>
/// Writes must be atomic and flushed to disk: this file is the only way back to the
/// user's own configuration, so a power cut mid-write must leave either the previous
/// snapshot or the new one, never half of either.
/// </remarks>
public interface ISessionSnapshotStore
{
    /// <summary>Null when there is no snapshot, which means no session is in
    /// progress and nothing is pending restoration.</summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAtomicAsync(string content, CancellationToken cancellationToken);

    /// <summary>Removes the snapshot. Called only once a revert has finished cleanly:
    /// while the file exists, the environment is assumed to still be modified.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);

    bool Exists();
}
