using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using SunshineHelper.Application.Controller;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Session;
using SunshineHelper.Infrastructure.Overlay;

namespace SunshineHelper.App.Services;

/// <summary>
/// Owns the overlay while it is open (docs/adr/0015-overlay-pelo-controle.md, PR F): opens the
/// window when the combo fires, reads the controller to move through it, runs "Restore now",
/// closes it (B, or the stream ending) and gives the focus back to what was in front. Everything
/// here runs on the UI thread. The rules of the menu are in <see cref="OverlayModel"/> and
/// <see cref="GamepadNavigator"/>, tested without a window.
/// </summary>
internal sealed class OverlayHost
{
    private static readonly TimeSpan _statusInterval = TimeSpan.FromSeconds(1);

    private readonly AppHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly GamepadNavigator _navigator = new();
    private GamepadPoller? _poller;
    private OverlayWindow? _window;
    private OverlayModel _model = new();
    private nint _previousForeground;
    private bool _takeFocus;
    private bool _busy;
    private bool _checkingStatus;
    private TimeSpan _nextStatusCheck;
    private string _statusText = string.Empty;
    private bool _statusIdle;
    private string? _note;
    private OverlayNoteKind _noteKind;

    public OverlayHost(AppHost host)
    {
        _host = host;
        _timer.Tick += (_, _) => OnTick();
    }

    /// <summary>True while the overlay window is on screen: the combo does not open it again.</summary>
    public bool IsOpen => _window is not null;

    /// <summary>Opens the overlay. Does nothing when it is already open.</summary>
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        var settings = _host.Settings.Current.Controller.Overlay;
        _takeFocus = settings.TakeFocus;
        _model = new OverlayModel();
        _busy = false;
        _note = null;
        _noteKind = OverlayNoteKind.None;
        _statusText = Text("Overlay.Status.Streaming");
        _statusIdle = false;
        _nextStatusCheck = TimeSpan.Zero;

        _previousForeground = _takeFocus ? WindowsForegroundFocus.GetForeground() : 0;
        _window = new OverlayWindow();
        _window.Open(_takeFocus);

        if (_takeFocus && !WindowsForegroundFocus.TryFocus(_window.Handle))
        {
            // Not an error: the overlay works either way, and the game may also get the buttons.
            _host.Log(DiagnosticLevel.Info, "overlay", "opened without the focus: Windows kept the game in front, so the controller buttons may also reach the game");
        }

        _poller ??= new GamepadPoller(_host.GamepadReader);
        var now = _clock.Elapsed;
        _navigator.Reset(_poller.Read(now));
        _host.Log(DiagnosticLevel.Info, "overlay", "opened");
        Render();
        _timer.Start();
    }

    /// <summary>Closes the overlay and, when it took the focus, gives it back.</summary>
    public void Close()
    {
        if (_window is null)
        {
            return;
        }

        _timer.Stop();
        var window = _window;
        _window = null;
        window.Close();

        if (_takeFocus && _previousForeground != 0 && !WindowsForegroundFocus.TryFocus(_previousForeground))
        {
            _host.Log(DiagnosticLevel.Info, "overlay", "closed; Windows did not give the focus back to the window that had it");
        }

        _previousForeground = 0;
        _host.Log(DiagnosticLevel.Info, "overlay", "closed");
    }

    private void OnTick()
    {
        if (_window is null || _poller is null)
        {
            return;
        }

        var now = _clock.Elapsed;
        var held = _poller.Read(now);
        bool changed = false;
        foreach (var press in _navigator.Update(held, now))
        {
            if (_busy)
            {
                continue;
            }

            switch (_model.Handle(press))
            {
                case OverlayRequest.Close:
                    Close();
                    return;
                case OverlayRequest.RestoreNow:
                    _ = RestoreNowAsync();
                    break;
                default:
                    break;
            }

            changed = true;
        }

        if (now >= _nextStatusCheck && !_checkingStatus)
        {
            _nextStatusCheck = now + _statusInterval;
            _ = RefreshStatusAsync();
        }

        if (changed)
        {
            Render();
        }
    }

    /// <summary>Looks at the session once a second: updates the line at the top and closes the
    /// overlay when the stream is over (unless the user restored from here, which clears the
    /// session on purpose).</summary>
    private async Task RefreshStatusAsync()
    {
        _checkingStatus = true;
        try
        {
            var status = await _host.Status.ReadAsync(CancellationToken.None);
            if (_window is null)
            {
                return;
            }

            if (_model.Restored)
            {
                _statusText = Text("Overlay.Status.Restored");
                _statusIdle = true;
            }
            else if (status.Kind == SessionStatusKind.Idle)
            {
                Close();
                return;
            }
            else
            {
                _statusText = StatusText(status);
                _statusIdle = false;
            }

            Render();
        }
        finally
        {
            _checkingStatus = false;
        }
    }

    private async Task RestoreNowAsync()
    {
        _busy = true;
        _note = Text("Overlay.Restoring");
        _noteKind = OverlayNoteKind.Info;
        Render();

        string? error = null;
        try
        {
            var response = await _host.RestoreNowAsync(CancellationToken.None);
            if (!response.Ok)
            {
                error = response.Error ?? string.Empty;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex.Message;
        }

        _busy = false;
        if (error is null)
        {
            _model.MarkRestored();
            _statusText = Text("Overlay.Status.Restored");
            _statusIdle = true;
            _note = Text("Overlay.Restored");
            _noteKind = OverlayNoteKind.Ok;
            _host.Log(DiagnosticLevel.Info, "overlay", "restore now: done");
        }
        else
        {
            _note = string.Format(CultureInfo.CurrentCulture, Text("Overlay.RestoreFailed"), error);
            _noteKind = OverlayNoteKind.Warn;
            _host.Log(DiagnosticLevel.Warning, "overlay", "restore now: " + error);
        }

        if (_window is not null)
        {
            Render();
        }
    }

    private string StatusText(SessionStatus status)
    {
        string? profile = status.ProfileId is null
            ? null
            : _host.Settings.Current.Profiles.FirstOrDefault(p => string.Equals(p.Id, status.ProfileId, StringComparison.Ordinal))?.Name;

        if (status.ClientName is { Length: > 0 } client && profile is not null)
        {
            return string.Format(CultureInfo.CurrentCulture, Text("Overlay.Status.ClientAndProfile"), client, profile);
        }

        return profile is not null
            ? string.Format(CultureInfo.CurrentCulture, Text("Overlay.Status.Profile"), profile)
            : Text("Overlay.Status.Streaming");
    }

    private void Render() =>
        _window?.Render(_model, new OverlayView(_statusText, _statusIdle, _note, _noteKind, _busy));

    private string Text(string key) =>
        System.Windows.Application.Current.TryFindResource(key) as string ?? key;
}
