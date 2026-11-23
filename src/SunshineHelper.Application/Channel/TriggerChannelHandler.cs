using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Protocol;

namespace SunshineHelper.Application.Channel;

/// <summary>
/// Turns one <see cref="TriggerRequest"/> into one <see cref="ChannelResponse"/>: picks the
/// profile and calls <see cref="SessionOrchestrator"/>. The pipe server in
/// SunshineHelper.Infrastructure owns the actual connection; this class has no I/O of its
/// own, so it is unit-testable with fake modules exactly like
/// <see cref="SessionOrchestrator"/> itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Serialized, not per-connection:</b> a single <see cref="SemaphoreSlim"/> ensures only
/// one <see cref="SessionOrchestrator"/> call ever runs at a time, however many pipe
/// connections arrive concurrently. <see cref="SessionOrchestrator"/> has no locking of its
/// own — <c>ApplyAsync</c> and <c>RestoreAsync</c> both read and rewrite the same snapshot
/// file in steps, so running two calls side by side could revert a module the other call
/// is mid-way through applying. This is core "núcleo" territory
/// (docs/adr/0003-canal-e-auxiliar-de-gatilho.md), not something a later PR should have to
/// discover by a race it hit in the field.
/// </para>
/// <para>
/// <b>The <c>waitMs</c> race is a reply deadline, not a work deadline:</b> the real
/// <see cref="SessionOrchestrator"/> call is handed <paramref name="serverLifetime"/> (tied
/// to the app itself, never to one connection), so it keeps running to completion even
/// after this method has already replied <see cref="ChannelState.Timeout"/> and the
/// auxiliary has moved on. The semaphore still serializes it correctly: the next op simply
/// waits its turn, same as the auxiliary would if it stayed around to ask again.
/// </para>
/// </remarks>
public sealed class TriggerChannelHandler : IDisposable
{
    private readonly SessionOrchestrator _orchestrator;
    private readonly ICurrentSettingsProvider _settingsProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDiagnosticLog _log;

    public TriggerChannelHandler(
        SessionOrchestrator orchestrator,
        ICurrentSettingsProvider settingsProvider,
        IDiagnosticLog? log = null)
    {
        _orchestrator = orchestrator;
        _settingsProvider = settingsProvider;
        _log = log ?? NullDiagnosticLog.Instance;
    }

    public void Dispose() => _gate.Dispose();

    /// <param name="serverLifetime">Cancelled only when the app itself is shutting down —
    /// never by the pipe connection going away, so a slow apply/revert is never abandoned
    /// just because the auxiliary stopped waiting.</param>
    public async Task<ChannelResponse> HandleAsync(TriggerRequest request, CancellationToken serverLifetime)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.V != ProtocolVersion.Current)
        {
            return new ChannelResponse
            {
                Ok = false,
                Error = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Versão de protocolo {request.V} não suportada (esperado {ProtocolVersion.Current})."),
            };
        }

        string clientText = request.Client is null
            ? "-"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"'{request.Client.Name}' {request.Client.Width}x{request.Client.Height} hdr {request.Client.Hdr}");
        _log.Write(
            DiagnosticLevel.Info,
            "channel",
            string.Create(
                CultureInfo.InvariantCulture,
                $"request received: op '{request.Op}', reason '{request.Reason ?? "-"}', client {clientText}"));

        var response = request.Op switch
        {
            TriggerOp.SessionStart => await RaceAsync(request, StartAsync, serverLifetime).ConfigureAwait(false),
            TriggerOp.SessionEnd => await RaceAsync(request, EndAsync, serverLifetime).ConfigureAwait(false),
            _ => new ChannelResponse { Ok = false, Error = $"Operação desconhecida: '{request.Op}'." },
        };

        string elapsedText = response.ElapsedMs is { } elapsedMs
            ? string.Create(CultureInfo.InvariantCulture, $"{elapsedMs} ms")
            : "-";
        _log.Write(
            response.Ok ? DiagnosticLevel.Info : DiagnosticLevel.Error,
            "channel",
            string.Create(
                CultureInfo.InvariantCulture,
                $"request answered: op '{request.Op}', ok {response.Ok}, state '{response.State}', profile '{response.Profile ?? "-"}', elapsed {elapsedText}{(response.Error is { } error ? ", error: " + error : string.Empty)}"));

        return response;
    }

    /// <summary>
    /// Ends the session only if the snapshot on disk still belongs to
    /// <paramref name="sessionId"/> when this call gets its turn. In-process only, for the
    /// Sunshine watchdog (docs/adr/0013-sunshine-encerrado-restaura.md): between deciding
    /// that a session is dead and getting through the gate, a new session-start may already
    /// have restored it and applied a new one, which must not be reverted by mistake.
    /// </summary>
    public async Task<ChannelResponse> EndSessionIfCurrentAsync(
        string sessionId,
        string reason,
        CancellationToken serverLifetime)
    {
        var request = new TriggerRequest { Op = TriggerOp.SessionEnd, Reason = reason };
        var response = await RaceAsync(
            request,
            async (endRequest, cancellationToken) =>
            {
                string? current = await _orchestrator.ReadSessionIdAsync(cancellationToken).ConfigureAwait(false);
                return string.Equals(current, sessionId, StringComparison.Ordinal)
                    ? await EndAsync(endRequest, cancellationToken).ConfigureAwait(false)
                    : new ChannelResponse { Ok = true, State = ChannelState.NothingToDo };
            },
            serverLifetime).ConfigureAwait(false);

        _log.Write(
            response.Ok ? DiagnosticLevel.Info : DiagnosticLevel.Error,
            "channel",
            string.Create(
                CultureInfo.InvariantCulture,
                $"in-process end answered: reason '{reason}', ok {response.Ok}, state '{response.State}'{(response.Error is { } error ? ", error: " + error : string.Empty)}"));

        return response;
    }

    private async Task<ChannelResponse> RaceAsync(
        TriggerRequest request,
        Func<TriggerRequest, CancellationToken, Task<ChannelResponse>> work,
        CancellationToken serverLifetime)
    {
        // Waits its turn with whatever session-start/session-end is already running.
        // Cancelled only by server shutdown, never by this connection's own lifetime.
        await _gate.WaitAsync(serverLifetime).ConfigureAwait(false);

        Task<ChannelResponse> workTask = RunGuardedAsync(request, work, serverLifetime);

        var timing = _settingsProvider.Current.Timing;
        int waitMs = Math.Clamp(request.WaitMs ?? timing.BarrierDefaultMs, 0, timing.BarrierCeilingMs);

        var finished = await Task.WhenAny(workTask, Task.Delay(waitMs, serverLifetime)).ConfigureAwait(false);

        return finished == workTask
            ? await workTask.ConfigureAwait(false)
            : new ChannelResponse { Ok = true, State = ChannelState.Timeout };
    }

    /// <summary>Releases the gate when the real work finishes, whenever that is — and
    /// never lets an exception escape as a faulted task, so a caller that stopped
    /// observing this task after a timeout reply can never crash the process with it.</summary>
    private async Task<ChannelResponse> RunGuardedAsync(
        TriggerRequest request,
        Func<TriggerRequest, CancellationToken, Task<ChannelResponse>> work,
        CancellationToken serverLifetime)
    {
        try
        {
            return await work(request, serverLifetime).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ChannelResponse { Ok = false, State = ChannelState.Failed, Error = ex.Message };
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ChannelResponse> StartAsync(TriggerRequest request, CancellationToken cancellationToken)
    {
        // Sunshine only runs the Prep Command for a new launch, so a snapshot still on disk
        // at that point belongs to a session that is over without its Undo ever running —
        // Sunshine restarted or crashed. It is put back first, with the same restore the
        // app runs at startup; the new session only starts if that worked, because the
        // snapshot is only deleted after a complete revert, and capturing over a modified
        // environment would lose the user's own values (docs/adr/0011). A /resume does not
        // prove the old session is dead, so it keeps the plain refusal.
        List<string> notes = [];
        if (!string.Equals(request.Reason, SessionStartReason.Resume, StringComparison.Ordinal)
            && _orchestrator.HasSessionOnDisk)
        {
            _log.Write(DiagnosticLevel.Warning, "channel", "session-start found a previous session on disk; restoring it first");
            var leftover = await _orchestrator.RestoreAsync(cancellationToken).ConfigureAwait(false);
            if (leftover.Status == RestoreStatus.Failed)
            {
                return new ChannelResponse
                {
                    Ok = false,
                    State = ChannelState.Failed,
                    Error = "Uma sessão anterior que não foi encerrada não pôde ser restaurada, então a nova não começou. "
                        + JoinProblems(leftover.Problems),
                };
            }

            notes.Add("Uma sessão anterior que não foi encerrada foi restaurada antes desta.");
            notes.AddRange(leftover.Problems);
        }

        var clientRequest = ToClientRequest(request.Client);
        var settings = _settingsProvider.Current;
        var profile = ClientProfileSelector.Select(settings, clientRequest);

        if (profile is null)
        {
            return new ChannelResponse
            {
                Ok = false,
                State = ChannelState.Failed,
                Error = JoinProblems([.. notes, "Nenhum perfil corresponde a este cliente, e não há perfil padrão configurado."]),
            };
        }

        var result = await _orchestrator.ApplyAsync(profile, clientRequest, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            SessionApplyStatus.Applied => new ChannelResponse
            {
                Ok = true,
                State = ChannelState.Applied,
                ElapsedMs = (int)result.Elapsed.TotalMilliseconds,
                Profile = profile.Name,
                // A module that could not be applied is a warning, not a failure: the
                // stream goes on with the rest of the profile (docs/adr/0011).
                Error = JoinProblems([.. notes, .. result.Problems]),
            },
            _ => new ChannelResponse
            {
                Ok = false,
                State = ChannelState.Failed,
                Error = JoinProblems([.. notes, result.ErrorMessage ?? "Falha desconhecida ao aplicar a sessão."]),
            },
        };
    }

    private async Task<ChannelResponse> EndAsync(TriggerRequest request, CancellationToken cancellationToken)
    {
        // A plain disconnect leaves the launcher open: the client may still /resume, and
        // Sunshine ends the stream on its own if the app it launched is gone.
        IReadOnlySet<string> modulesToLeave = string.Equals(request.Reason, SessionEndReason.Disconnect, StringComparison.Ordinal)
            ? new HashSet<string>(StringComparer.Ordinal) { LauncherModule.ModuleId }
            : new HashSet<string>(StringComparer.Ordinal);

        var result = await _orchestrator.RestoreAsync(modulesToLeave, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            RestoreStatus.NothingToDo => new ChannelResponse { Ok = true, State = ChannelState.NothingToDo },
            RestoreStatus.Restored => new ChannelResponse { Ok = true, State = ChannelState.Reverted },
            // Best effort already happened and the snapshot was cleared — what is left is
            // informational, not a reason to report failure to the auxiliary.
            RestoreStatus.Incomplete => new ChannelResponse
            {
                Ok = true,
                State = ChannelState.Reverted,
                Error = JoinProblems(result.Problems),
            },
            _ => new ChannelResponse
            {
                Ok = false,
                State = ChannelState.Failed,
                Error = JoinProblems(result.Problems),
            },
        };
    }

    private static ClientRequest ToClientRequest(ClientInfo? client) => new()
    {
        Name = client?.Name,
        Width = client?.Width,
        Height = client?.Height,
        Fps = client?.Fps,
        Hdr = client?.Hdr,
    };

    private static string? JoinProblems(IReadOnlyList<string> problems) =>
        problems.Count == 0 ? null : string.Join(" ", problems);
}
