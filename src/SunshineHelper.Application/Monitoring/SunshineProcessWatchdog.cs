using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Protocol;

namespace SunshineHelper.Application.Monitoring;

/// <summary>
/// Puts the environment back when the Sunshine process that was running during a session
/// is gone, because then its Undo Command will never run
/// (docs/adr/0013-sunshine-encerrado-restaura.md).
/// </summary>
/// <remarks>
/// <para>
/// It watches process ids, not the name: a Sunshine that restarted on its own has a new
/// id, and the stream it was serving is over all the same. The ids are noted the first
/// time a session is seen on disk; if no Sunshine process is running at that point (a fork
/// with another process name, say), the watchdog does nothing for that session rather than
/// restore in the middle of a stream it cannot see.
/// </para>
/// <para>
/// The restore goes through <see cref="TriggerChannelHandler.EndSessionIfCurrentAsync"/>,
/// so it waits its turn behind any start or end already running and is skipped if a new
/// session replaced this one in the meantime. One attempt per session: a restore that
/// fails leaves the snapshot as <see cref="SessionState.RevertFailed"/>, which the Dashboard
/// already reports, and retrying every few seconds would only repeat the failure.
/// </para>
/// <para>Driven by a single loop (<see cref="RunAsync"/>); not thread-safe across
/// concurrent calls to <see cref="CheckAsync"/>.</para>
/// </remarks>
public sealed class SunshineProcessWatchdog
{
    /// <summary>Checks in a row without the watched process before restoring, so a
    /// single odd reading never ends a session.</summary>
    public const int MissesBeforeRestore = 2;

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly ISunshineProcessProbe _probe;
    private readonly TriggerChannelHandler _handler;
    private readonly ISessionSnapshotStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly IDiagnosticLog _log;

    private string? _sessionId;
    private HashSet<int>? _watchedIds;
    private int _misses;
    private bool _handled;
    private bool _warnedNotFound;

    public SunshineProcessWatchdog(
        ISunshineProcessProbe probe,
        TriggerChannelHandler handler,
        ISessionSnapshotStore store,
        TimeProvider? timeProvider = null,
        IDiagnosticLog? log = null)
    {
        _probe = probe;
        _handler = handler;
        _store = store;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _log = log ?? NullDiagnosticLog.Instance;
    }

    /// <summary>Checks every <paramref name="interval"/> until <paramref
    /// name="cancellationToken"/> is cancelled. Meant to run for the lifetime of the app,
    /// in the background.</summary>
    public async Task RunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                try
                {
                    await CheckAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad check must not stop the watching for the rest of the app's life.
                    _log.Write(DiagnosticLevel.Error, "watchdog", "check failed: " + ex.Message);
                }

                await Task.Delay(interval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is shutting down; there is nothing left to watch.
        }
    }

    /// <summary>One check. True when it asked for the session to be restored.</summary>
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        // Outside a stream this is the only cost: one look at whether a file exists.
        if (!_store.Exists())
        {
            Forget();
            return false;
        }

        var snapshot = await TryReadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }

        if (!string.Equals(snapshot.SessionId, _sessionId, StringComparison.Ordinal))
        {
            Forget();
            _sessionId = snapshot.SessionId;
        }

        if (_handled || snapshot.State == SessionState.RevertFailed)
        {
            return false;
        }

        var running = _probe.FindProcessIds();
        if (_watchedIds is null)
        {
            StartWatching(running);
            return false;
        }

        if (IsAnyWatchedRunning(running))
        {
            _misses = 0;
            return false;
        }

        _misses++;
        if (_misses < MissesBeforeRestore)
        {
            return false;
        }

        _handled = true;
        _log.Write(DiagnosticLevel.Warning, "watchdog", "the Sunshine process of this session is gone; restoring the environment");
        await _handler
            .EndSessionIfCurrentAsync(snapshot.SessionId, SessionEndReason.SunshineExited, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private void StartWatching(IReadOnlyCollection<int> running)
    {
        if (running.Count == 0)
        {
            if (!_warnedNotFound)
            {
                _warnedNotFound = true;
                _log.Write(
                    DiagnosticLevel.Warning,
                    "watchdog",
                    "no Sunshine process found during this session; it will not be restored automatically if Sunshine stops");
            }

            return;
        }

        _watchedIds = [.. running];
        string count = running.Count.ToString(CultureInfo.InvariantCulture);
        _log.Write(DiagnosticLevel.Info, "watchdog", "watching " + count + " Sunshine process(es) for this session");
    }

    private bool IsAnyWatchedRunning(IReadOnlyCollection<int> running)
    {
        foreach (int id in running)
        {
            if (_watchedIds!.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<SessionSnapshot?> TryReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            string? content = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            return content is null ? null : SessionSnapshotSerializer.Deserialize(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SnapshotFormatException)
        {
            // An unreadable snapshot is already shown as "needs attention"; guessing here
            // could only make it worse.
            return null;
        }
    }

    private void Forget()
    {
        _sessionId = null;
        _watchedIds = null;
        _misses = 0;
        _handled = false;
        _warnedNotFound = false;
    }
}
