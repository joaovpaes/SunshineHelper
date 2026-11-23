using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Monitoring;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Protocol;

namespace SunshineHelper.Application.Monitoring;

/// <summary>
/// Watches the Sunshine log for the two things Sunshine itself never runs a command for —
/// a plain disconnect and a <c>/resume</c> — and reacts through the same
/// <see cref="TriggerChannelHandler"/> the pipe channel uses, so a disconnect-triggered
/// revert and a <c>/cancel</c>'s Undo Command can never run at the same time
/// (docs/adr/0004-leitor-do-log.md).
/// </summary>
/// <remarks>
/// <para>
/// This class only reacts; it never decides the device's identity or profile from
/// scratch. On <c>/resume</c> it reapplies by client name alone (the one detail
/// <see cref="SessionSnapshot"/> keeps once the rest of the session is gone), so it goes
/// through <see cref="ClientProfileSelector"/> again exactly as a fresh connection would.
/// </para>
/// <para>
/// <b>Not thread-safe across concurrent calls to <see cref="HandleAsync"/>.</b> It is
/// meant to be driven by a single loop reading <see cref="ISunshineLogSource"/> lines in
/// order (see <see cref="RunAsync"/>); the lines themselves arrive in the order Sunshine
/// wrote them, and the state machine depends on that order.
/// </para>
/// </remarks>
public sealed class SunshineSessionMonitor
{
    private readonly ISunshineLogSource _source;
    private readonly TriggerChannelHandler _handler;
    private readonly ISessionSnapshotStore _store;
    private readonly ICurrentSettingsProvider _settingsProvider;
    private readonly IDiagnosticLog _log;

    private SunshineSessionEvent? _lastEndingSignal;
    private string? _pendingResumeClientName;

    public SunshineSessionMonitor(
        ISunshineLogSource source,
        TriggerChannelHandler handler,
        ISessionSnapshotStore store,
        ICurrentSettingsProvider settingsProvider,
        IDiagnosticLog? log = null)
    {
        _log = log ?? NullDiagnosticLog.Instance;
        _source = source;
        _handler = handler;
        _store = store;
        _settingsProvider = settingsProvider;
    }

    /// <summary>Reads <see cref="ISunshineLogSource"/> until <paramref
    /// name="cancellationToken"/> is cancelled. Meant to run for the lifetime of the app,
    /// in the background.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in _source.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
            {
                var sunshineEvent = SunshineLogLineParser.TryParse(line);
                if (sunshineEvent is not null)
                {
                    await HandleAsync(sunshineEvent.Value, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is shutting down; there is nothing left to watch for.
        }
    }

    /// <summary>Processes one already-parsed event, exactly as <see cref="RunAsync"/>
    /// does for each recognized log line. Public, like
    /// <see cref="TriggerChannelHandler.HandleAsync"/>, so a caller — or a test — can
    /// drive the state machine one event at a time without going through a log
    /// source.</summary>
    public async Task HandleAsync(SunshineSessionEvent sunshineEvent, CancellationToken cancellationToken)
    {
        _log.Write(
            DiagnosticLevel.Info,
            "monitor",
            string.Create(CultureInfo.InvariantCulture, $"sunshine log event: {sunshineEvent}"));

        switch (sunshineEvent)
        {
            case SunshineSessionEvent.ClientConnected:
                // A fresh connection, not a /resume: whatever was pending is moot.
                _lastEndingSignal = null;
                break;

            case SunshineSessionEvent.ClientDisconnected:
            case SunshineSessionEvent.CancelRequested:
                // "Session ended" says nothing on its own about which of the two
                // preceded it (tests/fixtures/sunshine), so this is remembered here.
                _lastEndingSignal = sunshineEvent;
                break;

            case SunshineSessionEvent.SessionEnded:
                await HandleSessionEndedAsync(cancellationToken).ConfigureAwait(false);
                _lastEndingSignal = null;
                break;

            case SunshineSessionEvent.Resumed:
                await HandleResumedAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleSessionEndedAsync(CancellationToken cancellationToken)
    {
        if (_lastEndingSignal == SunshineSessionEvent.CancelRequested)
        {
            // The Undo Command over the pipe (ADR 0003) is the primary way this reverts.
            // This call is only a fallback for when that path did not run — the trigger
            // auxiliary failed to connect, for instance. TriggerChannelHandler.HandleAsync
            // goes through the exact same gate the pipe server uses, so if the Undo
            // Command already did the job, this finds no snapshot and comes back
            // nothing-to-do; it never races the primary path.
            await _handler
                .HandleAsync(new TriggerRequest { Op = TriggerOp.SessionEnd, Reason = SessionEndReason.Cancel }, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_lastEndingSignal != SunshineSessionEvent.ClientDisconnected)
        {
            // Session ended with no disconnect or cancel seen first — nothing recorded
            // to act on (could be this watcher starting up mid-session).
            return;
        }

        var (clientName, profile) = await ReadActiveSessionAsync(cancellationToken).ConfigureAwait(false);

        if (profile is not { RevertOnDisconnect: true })
        {
            // Default: keep the environment up, in case the client resumes
            // (docs/adr/0002-arquitetura-do-nucleo.md, "Desconexão e retomada").
            return;
        }

        // Remembered before reverting: once RestoreAsync succeeds, the snapshot — the
        // only other place this name was written down — is gone.
        _pendingResumeClientName = clientName;

        await _handler
            .HandleAsync(new TriggerRequest { Op = TriggerOp.SessionEnd, Reason = SessionEndReason.Disconnect }, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleResumedAsync(CancellationToken cancellationToken)
    {
        if (_store.Exists())
        {
            // The environment was never reverted; there is nothing to reapply.
            _pendingResumeClientName = null;
            return;
        }

        if (_pendingResumeClientName is null)
        {
            // Reverted for some other reason (a crash recovered at startup, a manual
            // --restore) with no profile this watcher applied itself. Not its call to
            // reapply something it never put there.
            return;
        }

        string clientName = _pendingResumeClientName;
        _pendingResumeClientName = null;

        await _handler
            .HandleAsync(
                new TriggerRequest
                {
                    Op = TriggerOp.SessionStart,
                    Client = new ClientInfo { Name = clientName },
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(string? ClientName, ClientProfile? Profile)> ReadActiveSessionAsync(
        CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return (null, null);
        }

        if (json is null)
        {
            return (null, null);
        }

        SessionSnapshot snapshot;
        try
        {
            snapshot = SessionSnapshotSerializer.Deserialize(json);
        }
        catch (SnapshotFormatException)
        {
            return (null, null);
        }

        var profile = snapshot.ProfileId is null
            ? null
            : _settingsProvider.Current.Profiles.FirstOrDefault(
                p => string.Equals(p.Id, snapshot.ProfileId, StringComparison.Ordinal));

        return (snapshot.ClientName, profile);
    }
}
