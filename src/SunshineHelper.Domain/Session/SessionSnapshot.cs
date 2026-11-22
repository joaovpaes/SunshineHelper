namespace SunshineHelper.Domain.Session;

/// <summary>
/// The whole record of one session, written to disk before anything is changed and
/// rewritten after every step, so there is always a way back
/// (docs/adr/0002-arquitetura-do-nucleo.md, "Snapshot atômico e orquestrador").
/// </summary>
public sealed record SessionSnapshot
{
    public required int SchemaVersion { get; init; }

    public required string SessionId { get; init; }

    public required BootIdentity Boot { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public required SessionState State { get; init; }

    /// <summary>Profile that was applied. Kept for diagnostics and for reapplying after
    /// a <c>/resume</c> (docs/adr/0002-arquitetura-do-nucleo.md, "Desconexão e
    /// retomada"); null when no profile matched.</summary>
    public string? ProfileId { get; init; }

    /// <summary>The <c>SUNSHINE_CLIENT_NAME</c> of the client that connected, for
    /// diagnostics. Any device Moonlight supports can appear here.</summary>
    public string? ClientName { get; init; }

    public required IReadOnlyList<CapturedModule> Modules { get; init; }

    /// <summary>A fresh snapshot, with every module still
    /// <see cref="ModuleApplyState.Pending"/> and the session
    /// <see cref="SessionState.Applying"/> — the state it has to be written in before
    /// the first module touches anything.</summary>
    public static SessionSnapshot Create(
        string sessionId,
        BootIdentity boot,
        DateTimeOffset capturedAt,
        IReadOnlyList<CapturedModule> modules,
        string? profileId = null,
        string? clientName = null) => new()
        {
            SchemaVersion = SnapshotSchema.CurrentVersion,
            SessionId = sessionId,
            Boot = boot,
            CapturedAt = capturedAt,
            State = SessionState.Applying,
            ProfileId = profileId,
            ClientName = clientName,
            Modules = modules,
        };

    public SessionSnapshot WithState(SessionState state) => this with { State = state };

    /// <summary>Replaces one module's <see cref="CapturedModule.ApplyState"/>, leaving
    /// every other module untouched.</summary>
    /// <exception cref="ArgumentException">No module with that id is in this snapshot.</exception>
    public SessionSnapshot WithModuleState(string moduleId, ModuleApplyState applyState)
    {
        int index = -1;
        for (int i = 0; i < Modules.Count; i++)
        {
            if (string.Equals(Modules[i].ModuleId, moduleId, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            throw new ArgumentException($"O snapshot não contém o módulo '{moduleId}'.", nameof(moduleId));
        }

        var updated = Modules.ToArray();
        updated[index] = updated[index] with { ApplyState = applyState };
        return this with { Modules = updated };
    }
}
