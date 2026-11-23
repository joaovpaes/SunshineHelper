using System.Globalization;
using System.Threading;
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
public sealed class TriggerChannelHandler
{
    private readonly SessionOrchestrator _orchestrator;
    private readonly ICurrentSettingsProvider _settingsProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TriggerChannelHandler(SessionOrchestrator orchestrator, ICurrentSettingsProvider settingsProvider)
    {
        _orchestrator = orchestrator;
        _settingsProvider = settingsProvider;
    }

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

        return request.Op switch
        {
            TriggerOp.SessionStart => await RaceAsync(request, serverLifetime, StartAsync).ConfigureAwait(false),
            TriggerOp.SessionEnd => await RaceAsync(request, serverLifetime, EndAsync).ConfigureAwait(false),
            _ => new ChannelResponse { Ok = false, Error = $"Operação desconhecida: '{request.Op}'." },
        };
    }

    private async Task<ChannelResponse> RaceAsync(
        TriggerRequest request,
        CancellationToken serverLifetime,
        Func<TriggerRequest, CancellationToken, Task<ChannelResponse>> work)
    {
        // Waits its turn with whatever session-start/session-end is already running.
        // Cancelled only by server shutdown, never by this connection's own lifetime.
        await _gate.WaitAsync(serverLifetime).ConfigureAwait(false);

        Task<ChannelResponse> workTask = RunGuardedAsync(request, serverLifetime, work);

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
        CancellationToken serverLifetime,
        Func<TriggerRequest, CancellationToken, Task<ChannelResponse>> work)
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
        var clientRequest = ToClientRequest(request.Client);
        var settings = _settingsProvider.Current;
        var profile = ClientProfileSelector.Select(settings, clientRequest);

        if (profile is null)
        {
            return new ChannelResponse
            {
                Ok = false,
                State = ChannelState.Failed,
                Error = "Nenhum perfil corresponde a este cliente, e não há perfil padrão configurado.",
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
            },
            _ => new ChannelResponse
            {
                Ok = false,
                State = ChannelState.Failed,
                Error = result.ErrorMessage ?? "Falha desconhecida ao aplicar a sessão.",
            },
        };
    }

    private async Task<ChannelResponse> EndAsync(TriggerRequest request, CancellationToken cancellationToken)
    {
        var result = await _orchestrator.RestoreAsync(cancellationToken).ConfigureAwait(false);

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
