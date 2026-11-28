using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.App;

/// <summary>
/// The Controller screen (docs/adr/0015-overlay-pelo-controle.md, PR E): for each combo, record
/// its buttons ("hold them and let go"), change the hold time and try it on a bar. The rules
/// live in <see cref="ComboCheck"/>, <see cref="ComboRecorder"/> and <see cref="ComboHoldMeter"/>
/// (tested); this file only draws the screen and reads the controller while it is open.
/// </summary>
public partial class MainWindow
{
    private const int HoldStepMs = 500;

    private readonly DispatcherTimer _controllerTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch _controllerClock = Stopwatch.StartNew();
    private readonly TimeSpan[] _controllerNextProbe = new TimeSpan[ComboDetector.MaxControllers];
    private readonly bool[] _controllerSeen = new bool[ComboDetector.MaxControllers];
    private readonly List<ComboPanel> _comboPanels = [];
    private readonly ComboRecorder _recorder = new();
    private ComboPanel? _recording;

    /// <summary>The controls of one combo and how to read and write it in the settings.</summary>
    private sealed class ComboPanel
    {
        public ComboPanel(
            Func<ControllerSettings, ComboSettings> get,
            Func<ControllerSettings, ComboSettings, ControllerSettings> set,
            Func<ControllerSettings, ComboSettings> getOther,
            ComboSettings defaultCombo)
        {
            Get = get;
            Set = set;
            GetOther = getOther;
            DefaultCombo = defaultCombo;
        }

        public Func<ControllerSettings, ComboSettings> Get { get; }

        public Func<ControllerSettings, ComboSettings, ControllerSettings> Set { get; }

        public Func<ControllerSettings, ComboSettings> GetOther { get; }

        public ComboSettings DefaultCombo { get; }

        public TextBlock ButtonsText { get; } = new();

        public TextBlock HoldText { get; } = new();

        public TextBlock BarText { get; } = new();

        public TextBlock Message { get; } = new();

        public System.Windows.Controls.Button RecordButton { get; } = new();

        public Border Track { get; } = new();

        public Border Fill { get; } = new();

        public ComboHoldMeter? Meter { get; set; }

        public GamepadButtons MeterButtons { get; set; }

        public int MeterHoldMs { get; set; }
    }

    private void BuildControllerPanels()
    {
        AddComboPanel(
            "Controller.CloseGame.Title",
            "Controller.CloseGame.Hint",
            new ComboPanel(
                c => c.CloseGame.Combo,
                (c, combo) => c with { CloseGame = c.CloseGame with { Combo = combo } },
                c => c.Overlay.Combo,
                new CloseGameSettings().Combo));
        AddComboPanel(
            "Controller.Overlay.Title",
            "Controller.Overlay.Hint",
            new ComboPanel(
                c => c.Overlay.Combo,
                (c, combo) => c with { Overlay = c.Overlay with { Combo = combo } },
                c => c.CloseGame.Combo,
                new OverlaySettings().Combo));
    }

    private void AddComboPanel(string titleKey, string hintKey, ComboPanel panel)
    {
        var separator = new Border
        {
            Margin = new Thickness(0, 24, 0, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        separator.SetResourceReference(Border.BorderBrushProperty, "Brush.Line");

        var title = new TextBlock { Margin = new Thickness(0, 24, 0, 0), Style = (Style)FindResource("Text.Label") };
        title.SetResourceReference(TextBlock.TextProperty, titleKey);
        var hint = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Style = (Style)FindResource("Text.Dim"), TextWrapping = TextWrapping.Wrap };
        hint.SetResourceReference(TextBlock.TextProperty, hintKey);

        panel.ButtonsText.Margin = new Thickness(0, 16, 0, 0);
        panel.ButtonsText.Style = (Style)FindResource("Text.Title");
        panel.ButtonsText.TextWrapping = TextWrapping.Wrap;

        panel.RecordButton.Padding = new Thickness(16, 8, 16, 8);
        panel.RecordButton.Style = (Style)FindResource("Button.Primary");
        panel.RecordButton.SetResourceReference(ContentControl.ContentProperty, "Controller.Record");
        panel.RecordButton.Click += (_, _) => OnRecordClicked(panel);

        var reset = new System.Windows.Controls.Button { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(16, 8, 16, 8), Style = (Style)FindResource("Button.Secondary") };
        reset.SetResourceReference(ContentControl.ContentProperty, "Controller.Reset");
        reset.Click += async (_, _) => await OnResetComboAsync(panel);

        var buttonRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        buttonRow.Children.Add(panel.RecordButton);
        buttonRow.Children.Add(reset);

        var less = new System.Windows.Controls.Button { Padding = new Thickness(12, 4, 12, 4), Style = (Style)FindResource("Button.Secondary") };
        less.SetResourceReference(ContentControl.ContentProperty, "Controller.Hold.Less");
        less.Click += async (_, _) => await OnChangeHoldAsync(panel, -HoldStepMs);
        var more = new System.Windows.Controls.Button { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 4, 12, 4), Style = (Style)FindResource("Button.Secondary") };
        more.SetResourceReference(ContentControl.ContentProperty, "Controller.Hold.More");
        more.Click += async (_, _) => await OnChangeHoldAsync(panel, HoldStepMs);

        panel.HoldText.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        panel.HoldText.Margin = new Thickness(0, 0, 16, 0);
        panel.HoldText.Style = (Style)FindResource("Text.Body");
        var holdRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        holdRow.Children.Add(panel.HoldText);
        holdRow.Children.Add(less);
        holdRow.Children.Add(more);

        panel.Track.Height = 8;
        panel.Track.Margin = new Thickness(0, 12, 0, 0);
        panel.Track.CornerRadius = new CornerRadius(4);
        panel.Track.SetResourceReference(Border.BackgroundProperty, "Brush.Line");
        panel.Fill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        panel.Fill.CornerRadius = new CornerRadius(4);
        panel.Fill.Width = 0;
        panel.Fill.SetResourceReference(Border.BackgroundProperty, "Brush.Accent");
        panel.Track.Child = panel.Fill;

        panel.BarText.Margin = new Thickness(0, 6, 0, 0);
        panel.BarText.Style = (Style)FindResource("Text.Dim");
        panel.Message.Margin = new Thickness(0, 8, 0, 0);
        panel.Message.Style = (Style)FindResource("Text.Dim");
        panel.Message.TextWrapping = TextWrapping.Wrap;

        // Before the last child (the shared message line at the end of the card).
        int at = ControllerHost.Children.IndexOf(ControllerMessage);
        foreach (UIElement element in new UIElement[] { separator, title, hint, panel.ButtonsText, buttonRow, holdRow, panel.Track, panel.BarText, panel.Message })
        {
            ControllerHost.Children.Insert(at++, element);
        }

        _comboPanels.Add(panel);
    }

    private void StartControllerScreen()
    {
        ControllerMessage.Text = string.Empty;
        _recording = null;
        foreach (var panel in _comboPanels)
        {
            panel.Message.Text = string.Empty;
            panel.Meter = null;
            ShowCombo(panel);
        }

        _controllerTimer.Start();
    }

    private void StopControllerScreen()
    {
        _controllerTimer.Stop();
        CancelRecording();
    }

    /// <summary>Puts a combo's buttons and hold time on screen, from the saved settings.</summary>
    private void ShowCombo(ComboPanel panel)
    {
        var combo = panel.Get(_host.Settings.Current.Controller);
        panel.ButtonsText.Text = DescribeButtons(combo.ToMask());
        panel.HoldText.Text = string.Format(CultureInfo.CurrentCulture, Text("Controller.Hold"), HoldSeconds(combo.HoldMs));
        panel.BarText.Text = Text("Controller.Hold.Idle");
        panel.RecordButton.SetResourceReference(ContentControl.ContentProperty, "Controller.Record");
    }

    private static string HoldSeconds(int holdMs) =>
        (holdMs / 1000.0).ToString("0.0", CultureInfo.CurrentCulture);

    private string DescribeButtons(GamepadButtons mask)
    {
        var names = ComboCheck.Split(mask).Select(button => Text("Button." + button));
        return string.Join(" + ", names);
    }

    private string DescribeIssues(IReadOnlyList<ComboIssue> issues)
    {
        var texts = issues.Select(issue => Text("Controller.Issue." + issue));
        return Text("Controller.NotSaved") + " " + string.Join(" ", texts);
    }

    private void OnRecordClicked(ComboPanel panel)
    {
        if (_recording == panel)
        {
            CancelRecording();
            return;
        }

        CancelRecording();
        if (!Array.Exists(_controllerSeen, seen => seen))
        {
            panel.Message.Text = Text("Controller.Record.NoController");
            return;
        }

        _recording = panel;
        _recorder.Reset();
        panel.Message.Text = string.Empty;
        panel.ButtonsText.Text = Text("Controller.Record.Prompt");
        panel.RecordButton.SetResourceReference(ContentControl.ContentProperty, "Controller.Record.Cancel");
    }

    private void CancelRecording()
    {
        if (_recording is { } panel)
        {
            _recording = null;
            ShowCombo(panel);
        }
    }

    /// <summary>One look at the controllers every 50 ms while the screen is open: records the
    /// combo, or moves the hold bars.</summary>
    private void OnControllerTick()
    {
        if (!IsVisible || ControllerCard.Visibility != Visibility.Visible)
        {
            return;
        }

        var held = ReadControllers();
        bool anyConnected = Array.Exists(_controllerSeen, seen => seen);
        ControllerStatusText.Text = Text(anyConnected ? "Controller.Status.Connected" : "Controller.Status.None");

        if (_recording is { } recording)
        {
            _recorder.Feed(held);
            if (_recorder.IsFinished)
            {
                _recording = null;
                SaveRecordedCombo(recording, _recorder.Recorded);
            }
            else if (_recorder.Recorded != GamepadButtons.None)
            {
                recording.ButtonsText.Text = DescribeButtons(_recorder.Recorded);
            }

            return;
        }

        foreach (var panel in _comboPanels)
        {
            UpdateHoldBar(panel, held);
        }
    }

    /// <summary>The buttons held on every controller together. A slot with nothing plugged in
    /// is looked at again only once a second: asking XInput about an empty slot is slow on
    /// some systems.</summary>
    private GamepadButtons ReadControllers()
    {
        var now = _controllerClock.Elapsed;
        var held = GamepadButtons.None;
        for (int i = 0; i < ComboDetector.MaxControllers; i++)
        {
            if (!_controllerSeen[i] && now < _controllerNextProbe[i])
            {
                continue;
            }

            var sample = _host.GamepadReader.Read(i);
            _controllerSeen[i] = sample.Connected;
            if (sample.Connected)
            {
                held |= sample.Buttons;
            }
            else
            {
                _controllerNextProbe[i] = now + TimeSpan.FromSeconds(1);
            }
        }

        return held;
    }

    private void UpdateHoldBar(ComboPanel panel, GamepadButtons held)
    {
        var combo = panel.Get(_host.Settings.Current.Controller);
        var mask = combo.ToMask();
        if (panel.Meter is null || panel.MeterButtons != mask || panel.MeterHoldMs != combo.HoldMs)
        {
            panel.MeterButtons = mask;
            panel.MeterHoldMs = combo.HoldMs;
            panel.Meter = combo.HoldMs > 0 ? new ComboHoldMeter(mask, TimeSpan.FromMilliseconds(combo.HoldMs)) : null;
        }

        double progress = panel.Meter?.Update(held, _controllerClock.Elapsed) ?? 0;
        panel.Fill.Width = panel.Track.ActualWidth * progress;
        panel.BarText.Text = Text(panel.Meter is { Reached: true } ? "Controller.Hold.Reached" : "Controller.Hold.Idle");
    }

    /// <summary>Checks the rules and saves a recorded combo; with a problem, nothing is saved
    /// and the old combo stays.</summary>
    private async void SaveRecordedCombo(ComboPanel panel, GamepadButtons recorded)
    {
        var current = _host.Settings.Current.Controller;
        var combo = panel.Get(current);
        var issues = ComboCheck.Check(recorded, combo.HoldMs, panel.GetOther(current).ToMask());
        if (issues.Count > 0)
        {
            ShowCombo(panel);
            panel.Message.Text = DescribeIssues(issues);
            return;
        }

        var buttons = ComboCheck.Split(recorded);
        bool saved = await TrySaveAsync(settings => settings with
        {
            Controller = panel.Set(settings.Controller, panel.Get(settings.Controller) with { Buttons = buttons }),
        });

        ShowCombo(panel);
        panel.Message.Text = saved
            ? string.Format(CultureInfo.CurrentCulture, Text("Controller.Saved"), DescribeButtons(recorded))
            : GeneralMessage.Text;
    }

    private async Task OnChangeHoldAsync(ComboPanel panel, int deltaMs)
    {
        CancelRecording();
        var current = _host.Settings.Current.Controller;
        int holdMs = Math.Clamp(panel.Get(current).HoldMs + deltaMs, ComboSettings.MinHoldMs, ComboSettings.MaxHoldMs);

        bool saved = await TrySaveAsync(settings => settings with
        {
            Controller = panel.Set(settings.Controller, panel.Get(settings.Controller) with { HoldMs = holdMs }),
        });

        ShowCombo(panel);
        panel.Message.Text = saved ? string.Empty : GeneralMessage.Text;
    }

    private async Task OnResetComboAsync(ComboPanel panel)
    {
        CancelRecording();
        var current = _host.Settings.Current.Controller;
        var issues = ComboCheck.Check(panel.DefaultCombo.ToMask(), panel.DefaultCombo.HoldMs, panel.GetOther(current).ToMask());
        if (issues.Count > 0)
        {
            panel.Message.Text = DescribeIssues(issues);
            return;
        }

        var defaults = panel.DefaultCombo;
        bool saved = await TrySaveAsync(settings => settings with
        {
            Controller = panel.Set(settings.Controller, defaults),
        });

        ShowCombo(panel);
        panel.Message.Text = saved
            ? string.Format(CultureInfo.CurrentCulture, Text("Controller.Saved"), DescribeButtons(defaults.ToMask()))
            : GeneralMessage.Text;
    }
}
