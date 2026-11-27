using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Application.Controller;

/// <summary>
/// Watches the controllers while a stream is on and reports the combos the user turned on
/// (docs/adr/0015-overlay-pelo-controle.md, "Leitura do controle"). In this first step the
/// report is a line in <c>app.log</c>; the action behind each combo comes in later PRs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only during a session.</b> Outside a session — and while no combo is turned on — the
/// loop only wakes up once a second to look at whether the session file exists, which is
/// what keeps the idle cost near zero. During a session it reads the controllers about
/// every 16 ms.
/// </para>
/// <para>
/// Slots with no controller are looked at again only once a second: asking XInput about an
/// empty slot is slow on some systems, and nobody plugs a controller in at 60 Hz.
/// </para>
/// <para>Settings are read on every pass, so turning the close-game combo on or changing
/// its buttons takes effect without restarting anything. Driven by one loop
/// (<see cref="RunAsync"/>); not thread-safe across concurrent calls to
/// <see cref="Step"/>.</para>
/// </remarks>
public sealed class ControllerWatcher
{
    /// <summary>How often the controllers are read while a session is on (about 60 Hz).</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>How often the loop looks for a session, and for a controller in an empty slot.</summary>
    public static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(1);

    private readonly IGamepadReader _reader;
    private readonly ISessionSnapshotStore _store;
    private readonly ICurrentSettingsProvider _settings;
    private readonly TimeProvider _time;
    private readonly IDiagnosticLog _log;
    private readonly long _startedAt;

    private readonly bool[] _connected = new bool[ComboDetector.MaxControllers];
    private readonly TimeSpan[] _nextLookup = new TimeSpan[ComboDetector.MaxControllers];

    private ComboDetector _detector = new([]);
    private IReadOnlyList<ComboDefinition> _combos = [];
    private TimeSpan _nextSessionCheck;
    private bool _sessionActive;

    public ControllerWatcher(
        IGamepadReader reader,
        ISessionSnapshotStore store,
        ICurrentSettingsProvider settings,
        TimeProvider? timeProvider = null,
        IDiagnosticLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);

        _reader = reader;
        _store = store;
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
        _log = log ?? NullDiagnosticLog.Instance;
        _startedAt = _time.GetTimestamp();
    }

    /// <summary>Raised on the loop's thread when a combo fires: the combo and the controller
    /// slot (0 to 3). Later PRs hang the real actions here.</summary>
    public event Action<ComboDefinition, int>? ComboFired;

    /// <summary>Runs until <paramref name="cancellationToken"/> is cancelled. Meant to live
    /// for the whole life of the app, in the background.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                TimeSpan wait;
                try
                {
                    wait = Step();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad pass must not stop the watching for the rest of the app's life.
                    _log.Write(DiagnosticLevel.Error, "controller", "read failed: " + ex.Message);
                    wait = SlowInterval;
                }

                await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is shutting down; there is nothing left to watch.
        }
    }

    /// <summary>One pass of the loop. Returns how long to wait before the next one.</summary>
    public TimeSpan Step()
    {
        TimeSpan now = _time.GetElapsedTime(_startedAt);

        IReadOnlyList<ComboDefinition> wanted = _settings.Current.Controller.ActiveCombos();
        if (wanted.Count == 0)
        {
            Stop();
            return SlowInterval;
        }

        if (now >= _nextSessionCheck)
        {
            _nextSessionCheck = now + SlowInterval;
            bool active = _store.Exists();
            if (active != _sessionActive)
            {
                _sessionActive = active;
                if (!active)
                {
                    Stop();
                }
            }
        }

        if (!_sessionActive)
        {
            return SlowInterval;
        }

        if (!wanted.SequenceEqual(_combos))
        {
            Adopt(wanted);
        }

        for (int index = 0; index < ComboDetector.MaxControllers; index++)
        {
            ReadSlot(index, now);
        }

        return PollInterval;
    }

    private void ReadSlot(int index, TimeSpan now)
    {
        if (!_connected[index] && now < _nextLookup[index])
        {
            return;
        }

        GamepadSample sample = _reader.Read(index);
        if (!sample.Connected)
        {
            if (_connected[index])
            {
                _connected[index] = false;
                _detector.Disconnect(index);
                _log.Write(DiagnosticLevel.Info, "controller", string.Create(CultureInfo.InvariantCulture, $"controller {index} disconnected"));
            }

            _nextLookup[index] = now + SlowInterval;
            return;
        }

        if (!_connected[index])
        {
            _connected[index] = true;
            _log.Write(DiagnosticLevel.Info, "controller", string.Create(CultureInfo.InvariantCulture, $"controller {index} connected (Xbox-style, read by XInput)"));
        }

        ComboDefinition? fired = _detector.Update(index, sample.Buttons, now);
        if (fired is not null)
        {
            _log.Write(
                DiagnosticLevel.Info,
                "controller",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"combo '{fired.Id}' fired on controller {index} (held {fired.Hold.TotalMilliseconds:0} ms)"));
            ComboFired?.Invoke(fired, index);
        }
    }

    private void Adopt(IReadOnlyList<ComboDefinition> wanted)
    {
        _combos = wanted;
        _detector = new ComboDetector(wanted);
        string ids = string.Join(", ", wanted.Select(combo => combo.Id));
        _log.Write(
            DiagnosticLevel.Info,
            "controller",
            string.Create(CultureInfo.InvariantCulture, $"watching the controllers for {wanted.Count} combo(s): {ids}"));
    }

    /// <summary>Nothing to read: forget what was seen, so the next session starts clean.</summary>
    private void Stop()
    {
        if (_combos.Count == 0 && !_connected.Any(c => c))
        {
            return;
        }

        _combos = [];
        _detector = new ComboDetector([]);
        Array.Clear(_connected);
        Array.Clear(_nextLookup);
    }
}
