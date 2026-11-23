using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Session;

/// <summary>
/// Runs a session: capture the environment, write it down, apply the profile, and put
/// everything back afterwards — or after a crash, or after a reboot
/// (docs/adr/0002-arquitetura-do-nucleo.md, "Snapshot atômico e orquestrador").
/// </summary>
/// <remarks>
/// <para>
/// Two rules shape everything here. Nothing is changed before the snapshot is safely on
/// disk, because without it there is no way back. And the snapshot is rewritten around
/// every single step, because a crash halfway through has to leave behind an accurate
/// record of which modules actually touched the environment: reverting a module that was
/// never applied would move a value the profile said to leave alone.
/// </para>
/// <para>
/// Modules are never trusted to behave. One that throws is treated exactly like one that
/// reports a failure. A module that fails to apply is put back on its own and the session
/// goes on with the others, as a warning (docs/adr/0011-falha-parcial-e-sessao-presa.md):
/// a missing FPS cap should not cost the user the resolution and HDR as well. Everything
/// goes back at once only when there is no safe way forward — the state could not be
/// read, the snapshot could not be written, the failed module could not be put back, or
/// the apply itself was cancelled.
/// </para>
/// </remarks>
public sealed class SessionOrchestrator
{
    private readonly IEnvironmentModule[] _modules;
    private readonly Dictionary<string, IEnvironmentModule> _modulesById;
    private readonly ISessionSnapshotStore _store;
    private readonly IBootIdentityProvider _bootIdentityProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IDiagnosticLog _log;

    public SessionOrchestrator(
        IEnumerable<IEnvironmentModule> modules,
        ISessionSnapshotStore store,
        IBootIdentityProvider bootIdentityProvider,
        IDiagnosticLog? log = null)
        : this(modules, store, bootIdentityProvider, TimeProvider.System, log)
    {
    }

    public SessionOrchestrator(
        IEnumerable<IEnvironmentModule> modules,
        ISessionSnapshotStore store,
        IBootIdentityProvider bootIdentityProvider,
        TimeProvider timeProvider,
        IDiagnosticLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _modules = modules.OrderBy(module => module.Order).ToArray();
        _modulesById = _modules.ToDictionary(module => module.Id, StringComparer.Ordinal);
        _store = store;
        _bootIdentityProvider = bootIdentityProvider;
        _timeProvider = timeProvider;
        _log = log ?? NullDiagnosticLog.Instance;
    }

    /// <summary>Applies <paramref name="profile"/> for the client in
    /// <paramref name="request"/>, after writing down everything it is about to
    /// change.</summary>
    public async Task<SessionApplyResult> ApplyAsync(
        ClientProfile profile,
        ClientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);

        long startedAt = _timeProvider.GetTimestamp();

        // A snapshot already on disk means a session is in progress or was left behind.
        // Capturing now would record the environment this app itself changed as if it
        // were the user's own, and the way back would be lost for good.
        if (_store.Exists())
        {
            Log(DiagnosticLevel.Warning, "apply refused: a session snapshot is already on disk");
            return new SessionApplyResult
            {
                Status = SessionApplyStatus.Refused,
                ErrorMessage = "Já existe uma sessão registrada. Restaure a anterior antes de começar outra.",
                Elapsed = Elapsed(startedAt),
            };
        }

        var desired = DesiredEnvironmentResolver.Resolve(profile, request);
        Log(
            DiagnosticLevel.Info,
            string.Create(
                CultureInfo.InvariantCulture,
                $"apply start: profile '{profile.Id}', resolution {Describe(desired.Resolution)}, hdr {Describe(desired.Hdr)}, fps limit {Describe(desired.FpsLimit)}, launcher delay {Describe(desired.LauncherDelayMs)}"));

        var captured = new List<CapturedModule>(_modules.Length);
        foreach (var module in _modules)
        {
            ModuleSnapshot moduleSnapshot;
            try
            {
                moduleSnapshot = await module.CaptureAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Nothing has been changed yet, and a module whose state cannot be read
                // has no way back: the session does not start.
                return ApplyFailed(
                    startedAt,
                    $"Não foi possível ler o estado atual de '{module.Id}': {ex.Message}",
                    environmentRestored: true);
            }

            captured.Add(new CapturedModule
            {
                ModuleId = module.Id,
                Order = module.Order,
                SurvivesReboot = module.SurvivesReboot,
                ApplyState = ModuleApplyState.Pending,
                Snapshot = moduleSnapshot,
            });
        }

        var snapshot = SessionSnapshot.Create(
            NewSessionId(),
            CurrentBoot() ?? BootIdentity.Unknown,
            _timeProvider.GetUtcNow(),
            captured,
            profile.Id,
            request.Name);

        string? writeError = await TryWriteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (writeError is not null)
        {
            return ApplyFailed(
                startedAt,
                $"Não foi possível gravar o snapshot da sessão: {writeError}",
                environmentRestored: true);
        }

        List<string> warnings = [];
        foreach (var module in _modules)
        {
            snapshot = snapshot.WithModuleState(module.Id, ModuleApplyState.Applying);

            // Marked before the module runs, so a crash inside it still leaves a record
            // that this module may be half-applied.
            writeError = await TryWriteAsync(snapshot, cancellationToken).ConfigureAwait(false);
            if (writeError is not null)
            {
                snapshot = snapshot.WithModuleState(module.Id, ModuleApplyState.Pending);
                return await RollbackAsync(
                    snapshot,
                    startedAt,
                    $"Não foi possível gravar o snapshot antes de aplicar '{module.Id}': {writeError}",
                    cancellationToken).ConfigureAwait(false);
            }

            ApplyOutcome outcome;
            try
            {
                outcome = await module.ApplyAsync(desired, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Includes cancellation: an interrupted apply still has to be undone.
                outcome = ApplyOutcome.Failed(ex.Message);
            }

            Log(
                outcome.Status == ApplyStatus.Failed ? DiagnosticLevel.Error : DiagnosticLevel.Info,
                string.Create(CultureInfo.InvariantCulture, $"apply module '{module.Id}': {outcome.Status}{(outcome.ErrorMessage is { } failure ? " - " + failure : string.Empty)}"));

            snapshot = snapshot.WithModuleState(module.Id, ToApplyState(outcome.Status));

            // A failure here is not fatal: the in-memory snapshot is accurate, and the
            // next write may well succeed.
            _ = await TryWriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

            if (outcome.Status == ApplyStatus.Failed)
            {
                string failureMessage = $"Falha ao aplicar '{module.Id}': {outcome.ErrorMessage}";

                // Cancelled means the app is going away, not that this module has a
                // problem: nothing more is applied and everything goes back, as before.
                if (cancellationToken.IsCancellationRequested)
                {
                    return await RollbackAsync(snapshot, startedAt, failureMessage, cancellationToken).ConfigureAwait(false);
                }

                var entry = snapshot.Modules.First(item => item.ModuleId == module.Id);
                var undo = await RevertModuleAsync(module, entry, cancellationToken).ConfigureAwait(false);
                if (!undo.Reverted)
                {
                    // Its state is unknown now, so the session cannot simply go on without
                    // it. Everything goes back; if this module still cannot, the snapshot
                    // stays for a later attempt, exactly as before.
                    return await RollbackAsync(
                        snapshot,
                        startedAt,
                        $"{failureMessage} (e não foi possível desfazê-lo: {undo.ErrorMessage})",
                        cancellationToken).ConfigureAwait(false);
                }

                snapshot = snapshot.WithModuleState(module.Id, ModuleApplyState.RolledBack);
                _ = await TryWriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

                Log(DiagnosticLevel.Warning, $"module '{module.Id}' put back on its own; the session goes on without it");
                warnings.Add($"'{module.Id}' não foi aplicado nesta sessão: {outcome.ErrorMessage}");
                if (undo.ErrorMessage is { } undoWarning)
                {
                    warnings.Add($"'{module.Id}': {undoWarning}");
                }
            }
        }

        snapshot = snapshot.WithState(SessionState.Applied);
        string warningSuffix = warnings.Count == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $", with {warnings.Count} warning(s)");
        Log(
            warnings.Count == 0 ? DiagnosticLevel.Info : DiagnosticLevel.Warning,
            string.Create(CultureInfo.InvariantCulture, $"apply done in {Elapsed(startedAt).TotalMilliseconds:0} ms{warningSuffix}"));
        string? finalWriteError = await TryWriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

        List<string> problems = [.. warnings];
        if (finalWriteError is not null)
        {
            problems.Add($"A sessão foi aplicada, mas o snapshot não pôde ser atualizado: {finalWriteError}");
        }

        return new SessionApplyResult
        {
            Status = SessionApplyStatus.Applied,
            Elapsed = Elapsed(startedAt),
            Problems = problems,
        };
    }

    /// <summary>
    /// Puts the environment back from the snapshot on disk. The same path serves the end
    /// of a session, the startup check and <c>--restore</c>: one way back means one set
    /// of ways to get it wrong.
    /// </summary>
    public Task<SessionRestoreResult> RestoreAsync(CancellationToken cancellationToken = default) =>
        RestoreAsync(new HashSet<string>(StringComparer.Ordinal), cancellationToken);

    /// <summary>
    /// Same as <see cref="RestoreAsync(CancellationToken)"/>, but the modules in
    /// <paramref name="modulesToLeave"/> are not reverted. Used for a plain disconnect
    /// with "revert on disconnect" on: the screen goes back to the user, but the launcher
    /// stays open so a <c>/resume</c> still has an app to return to
    /// (docs/adr/0008-modulo-launcher.md). The snapshot is still cleared afterwards — what
    /// was left alone is, from then on, the user's.
    /// </summary>
    public async Task<SessionRestoreResult> RestoreAsync(
        IReadOnlySet<string> modulesToLeave,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modulesToLeave);

        string? json;
        try
        {
            json = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return RestoreFailed($"Não foi possível ler o snapshot da sessão: {ex.Message}");
        }

        if (json is null)
        {
            Log(DiagnosticLevel.Info, "restore: no snapshot on disk, nothing to do");
            return new SessionRestoreResult { Status = RestoreStatus.NothingToDo };
        }

        SessionSnapshot snapshot;
        try
        {
            snapshot = SessionSnapshotSerializer.Deserialize(json);
        }
        catch (SnapshotFormatException ex)
        {
            // The file stays: it is the only record of the user's own settings, and a
            // format this build cannot read is not a reason to throw it away.
            return RestoreFailed(ex.Message);
        }

        var plan = RecoveryDecider.Decide(snapshot, CurrentBoot(), _timeProvider.GetUtcNow());
        Log(
            DiagnosticLevel.Info,
            string.Create(
                CultureInfo.InvariantCulture,
                $"restore start: {plan.ModulesToRevert.Count} module(s) to revert, reboot detected: {plan.RebootDetected}, leaving: [{string.Join(", ", modulesToLeave)}]"));

        if (plan.IsEmpty)
        {
            _ = await TryDeleteAsync(cancellationToken).ConfigureAwait(false);
            return new SessionRestoreResult
            {
                Status = RestoreStatus.Restored,
                RebootDetected = plan.RebootDetected,
            };
        }

        _ = await TryWriteAsync(snapshot.WithState(SessionState.Reverting), cancellationToken).ConfigureAwait(false);

        var (anyFailed, problems) = await RevertAsync(plan, modulesToLeave, cancellationToken).ConfigureAwait(false);

        if (anyFailed)
        {
            _ = await TryWriteAsync(snapshot.WithState(SessionState.RevertFailed), cancellationToken).ConfigureAwait(false);
            return new SessionRestoreResult
            {
                Status = RestoreStatus.Failed,
                RebootDetected = plan.RebootDetected,
                Problems = problems,
            };
        }

        string? deleteError = await TryDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (deleteError is not null)
        {
            problems.Add($"O ambiente voltou ao normal, mas o snapshot não pôde ser apagado: {deleteError}");
        }

        return new SessionRestoreResult
        {
            Status = problems.Count == 0 ? RestoreStatus.Restored : RestoreStatus.Incomplete,
            RebootDetected = plan.RebootDetected,
            Problems = problems,
        };
    }

    private async Task<SessionApplyResult> RollbackAsync(
        SessionSnapshot snapshot,
        long startedAt,
        string error,
        CancellationToken cancellationToken)
    {
        var plan = RecoveryDecider.Decide(snapshot, CurrentBoot(), _timeProvider.GetUtcNow());
        var (anyFailed, problems) = await RevertAsync(plan, new HashSet<string>(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);

        if (anyFailed)
        {
            _ = await TryWriteAsync(snapshot.WithState(SessionState.RevertFailed), cancellationToken).ConfigureAwait(false);
            return ApplyFailed(startedAt, error, environmentRestored: false, problems);
        }

        string? deleteError = await TryDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (deleteError is not null)
        {
            problems.Add($"O ambiente voltou ao normal, mas o snapshot não pôde ser apagado: {deleteError}");
        }

        return ApplyFailed(startedAt, error, environmentRestored: true, problems);
    }

    private async Task<(bool AnyFailed, List<string> Problems)> RevertAsync(
        RecoveryPlan plan,
        IReadOnlySet<string> modulesToLeave,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        bool anyFailed = false;

        foreach (var captured in plan.ModulesToRevert)
        {
            if (modulesToLeave.Contains(captured.ModuleId))
            {
                continue;
            }

            if (!_modulesById.TryGetValue(captured.ModuleId, out var module))
            {
                // Nothing here can act on it any more, and keeping the snapshot for its
                // sake would block every future session. Reported, not treated as a
                // failure.
                problems.Add($"O módulo '{captured.ModuleId}' não existe mais nesta versão do app e não pôde ser restaurado.");
                continue;
            }

            var outcome = await RevertModuleAsync(module, captured, cancellationToken).ConfigureAwait(false);

            if (!outcome.Reverted)
            {
                // Every module gets its turn even after one fails: the ones that can go
                // back should go back.
                anyFailed = true;
                problems.Add($"Não foi possível restaurar '{captured.ModuleId}': {outcome.ErrorMessage}");
            }
            else if (outcome.ErrorMessage is { } warning)
            {
                problems.Add($"'{captured.ModuleId}': {warning}");
            }
        }

        return (anyFailed, problems);
    }

    /// <summary>Reverts one module, never throwing: an exception counts as a failure.</summary>
    private async Task<RevertOutcome> RevertModuleAsync(
        IEnvironmentModule module,
        CapturedModule captured,
        CancellationToken cancellationToken)
    {
        RevertOutcome outcome;
        try
        {
            outcome = await module.RevertAsync(captured.Snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            outcome = RevertOutcome.Failed(ex.Message);
        }

        Log(
            outcome.Reverted ? DiagnosticLevel.Info : DiagnosticLevel.Error,
            string.Create(CultureInfo.InvariantCulture, $"revert module '{captured.ModuleId}': {(outcome.Reverted ? "ok" : "FAILED")}{(outcome.ErrorMessage is { } detail ? " - " + detail : string.Empty)}"));

        return outcome;
    }

    private async Task<string?> TryWriteAsync(SessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            string json = SessionSnapshotSerializer.Serialize(snapshot);
            await _store.WriteAtomicAsync(json, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<string?> TryDeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private BootIdentity? CurrentBoot()
    {
        try
        {
            return _bootIdentityProvider.GetCurrent();
        }
        catch (Exception)
        {
            // Unknown counts as "same boot", which restores everything.
            return null;
        }
    }

    private void Log(DiagnosticLevel level, string message) => _log.Write(level, "orchestrator", message);

    private static string Describe(Resolution? value) => value is null
        ? "unchanged"
        : string.Create(CultureInfo.InvariantCulture, $"{value.Width}x{value.Height}");

    private static string Describe<T>(T? value) => value is null ? "unchanged" : string.Create(CultureInfo.InvariantCulture, $"{value}");

    private TimeSpan Elapsed(long startedAt) => _timeProvider.GetElapsedTime(startedAt);

    private SessionApplyResult ApplyFailed(
        long startedAt,
        string error,
        bool environmentRestored,
        IReadOnlyList<string>? problems = null) => new()
        {
            Status = SessionApplyStatus.Failed,
            ErrorMessage = error,
            EnvironmentRestored = environmentRestored,
            Elapsed = Elapsed(startedAt),
            Problems = problems ?? [],
        };

    private static SessionRestoreResult RestoreFailed(string error) => new()
    {
        Status = RestoreStatus.Failed,
        Problems = [error],
    };

    private static ModuleApplyState ToApplyState(ApplyStatus status) => status switch
    {
        ApplyStatus.Changed => ModuleApplyState.Applied,
        ApplyStatus.NothingToDo => ModuleApplyState.Skipped,
        _ => ModuleApplyState.Failed,
    };

    private static string NewSessionId() => Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
}
