using System.Threading;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Session;

/// <summary>
/// Reads the session state straight from the snapshot file. The snapshot is already the
/// single record of "is the environment modified" (docs/adr/0002-arquitetura-do-nucleo.md),
/// and both the pipe and the log monitor write it through the orchestrator — so reading
/// it shows the right thing whichever path started the session, with no second source of
/// truth to keep in step and nothing added to the orchestrator.
/// </summary>
public sealed class SessionStatusReader
{
    private readonly ISessionSnapshotStore _store;

    public SessionStatusReader(ISessionSnapshotStore store)
    {
        _store = store;
    }

    public async Task<SessionStatus> ReadAsync(CancellationToken cancellationToken)
    {
        string? content;
        try
        {
            content = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return new SessionStatus(SessionStatusKind.NeedsAttention);
        }

        if (content is null)
        {
            return new SessionStatus(SessionStatusKind.Idle);
        }

        SessionSnapshot snapshot;
        try
        {
            snapshot = SessionSnapshotSerializer.Deserialize(content);
        }
        catch (SnapshotFormatException)
        {
            return new SessionStatus(SessionStatusKind.NeedsAttention);
        }

        var kind = snapshot.State == SessionState.RevertFailed
            ? SessionStatusKind.NeedsAttention
            : SessionStatusKind.Active;

        return new SessionStatus(kind, snapshot.ClientName, snapshot.ProfileId, snapshot.CapturedAt)
        {
            NotAppliedModules =
            [
                .. snapshot.Modules
                    .Where(entry => entry.ApplyState == ModuleApplyState.RolledBack)
                    .OrderBy(entry => entry.Order)
                    .Select(entry => entry.ModuleId),
            ],
        };
    }
}
