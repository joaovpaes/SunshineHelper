using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SunshineHelper.Application.Controller;

namespace SunshineHelper.App;

/// <summary>The kind of the line under the list, which sets its color.</summary>
internal enum OverlayNoteKind
{
    None,
    Info,
    Ok,
    Warn,
}

/// <summary>What the overlay shows besides the menu: the status line and the note.</summary>
internal sealed record OverlayView(string StatusText, bool StatusIdle, string? Note, OverlayNoteKind NoteKind, bool Busy);

/// <summary>
/// The overlay window (docs/adr/0015-overlay-pelo-controle.md, PR F): a dark panel on the right
/// third of the main screen, always on top, over a dimmed screen. It has no buttons of its own:
/// the controller moves through it (see <see cref="OverlayHost"/>), so it never needs the focus
/// to work. Everything is drawn from an <see cref="OverlayModel"/> on each change.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double BaseHeight = 1080;
    private const double PanelBaseWidth = 640;

    private static readonly System.Windows.Media.SolidColorBrush _surface = Rgb("#10151F");
    private static readonly System.Windows.Media.SolidColorBrush _surfaceRaised = Rgb("#161C28");
    private static readonly System.Windows.Media.SolidColorBrush _line = Rgb("#232B3A");
    private static readonly System.Windows.Media.SolidColorBrush _text = Rgb("#E8ECF4");
    private static readonly System.Windows.Media.SolidColorBrush _dim = Rgb("#98A2B5");
    private static readonly System.Windows.Media.SolidColorBrush _accent = Rgb("#5A97FF");
    private static readonly System.Windows.Media.SolidColorBrush _accentTint = Rgb("#082554");
    private static readonly System.Windows.Media.SolidColorBrush _ok = Rgb("#3DDC97");
    private static readonly System.Windows.Media.SolidColorBrush _warn = Rgb("#FFB020");
    private static readonly System.Windows.Media.SolidColorBrush _danger = Rgb("#FF5C6C");
    private static readonly System.Windows.Media.SolidColorBrush _ground = Rgb("#05070B");

    public OverlayWindow()
    {
        InitializeComponent();

        // The main screen, in device-independent units.
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        // Designed for a 1080p-high screen; a smaller or bigger screen scales it.
        double scale = Math.Clamp(Height / BaseHeight, 0.6, 2.5);
        PanelBorder.Width = Math.Min(Width, PanelBaseWidth * scale);
        PanelHost.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        ConfirmLayer.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
    }

    /// <summary>The window's handle, to give it the focus. Created on first use.</summary>
    internal nint Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Shows the window. With <paramref name="takeFocus"/> false it opens without being
    /// activated, so the game keeps the focus.</summary>
    internal void Open(bool takeFocus)
    {
        ShowActivated = takeFocus;
        Show();
    }

    /// <summary>Draws the menu and the question from the model.</summary>
    internal void Render(OverlayModel model, OverlayView view)
    {
        PanelHost.Children.Clear();
        PanelHost.Children.Add(BuildStatus(view));
        PanelHost.Children.Add(Block(Text("Overlay.Title"), 34, _text, bold: true, margin: new Thickness(0, 22, 0, 0)));
        PanelHost.Children.Add(Block(Text("Overlay.Subtitle"), 17, _dim, margin: new Thickness(0, 4, 0, 14)));

        foreach (var item in OverlayModel.Items)
        {
            PanelHost.Children.Add(BuildRow(item, model));
        }

        if (view.Note is { Length: > 0 } note)
        {
            PanelHost.Children.Add(BuildNote(note, view.NoteKind));
        }

        PanelHost.Children.Add(BuildFooter(model, view));
        RenderConfirm(model);
    }

    private static System.Windows.Media.SolidColorBrush Rgb(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    private string Text(string key) => TryFindResource(key) as string ?? key;

    private static TextBlock Block(string text, double size, System.Windows.Media.Brush color, bool bold = false, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = color,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0),
    };

    private static Border BuildStatus(OverlayView view)
    {
        var dot = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = view.StatusIdle ? _dim : _ok,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        var text = Block(view.StatusText, 17, _dim);

        var row = new DockPanel();
        DockPanel.SetDock(dot, Dock.Left);
        row.Children.Add(dot);
        row.Children.Add(text);

        return new Border
        {
            Margin = new Thickness(32, 28, 32, 0),
            Padding = new Thickness(16, 12, 16, 12),
            Background = _surface,
            BorderBrush = _line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = row,
        };
    }

    private Border BuildRow(OverlayItem item, OverlayModel model)
    {
        bool available = model.IsAvailable(item);
        bool selected = model.Selected == item;

        var name = Block(Text("Overlay.Item." + item), 24, _text);
        var description = Block(Text("Overlay.Item." + item + ".Description"), 16, _dim);
        var texts = new StackPanel();
        texts.Children.Add(name);
        texts.Children.Add(description);

        var content = new DockPanel();
        if (!available)
        {
            var badge = new Border
            {
                Background = _accentTint,
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Child = Block(Text("Overlay.Soon"), 13, _accent, bold: true),
            };
            DockPanel.SetDock(badge, Dock.Right);
            content.Children.Add(badge);
        }

        content.Children.Add(texts);

        return new Border
        {
            Margin = new Thickness(32, 0, 32, 8),
            Padding = new Thickness(20, 12, 20, 12),
            Background = selected ? _surfaceRaised : _surface,
            BorderBrush = selected ? _accent : System.Windows.Media.Brushes.Transparent,
            BorderThickness = selected ? new Thickness(6, 1, 1, 1) : new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Opacity = available ? 1 : 0.45,
            Child = content,
        };
    }

    private static Border BuildNote(string note, OverlayNoteKind kind)
    {
        var accent = kind switch
        {
            OverlayNoteKind.Ok => _ok,
            OverlayNoteKind.Warn => _warn,
            _ => _line,
        };

        return new Border
        {
            Margin = new Thickness(32, 8, 32, 0),
            Padding = new Thickness(16, 12, 16, 12),
            Background = _ground,
            BorderBrush = accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = Block(note, 17, _dim),
        };
    }

    private Border BuildFooter(OverlayModel model, OverlayView view)
    {
        var hints = new WrapPanel { Margin = new Thickness(32, 24, 32, 20) };
        if (model.Confirming || view.Busy)
        {
            // The question has its own hints; while it works, nothing is offered.
            return new Border { Child = hints };
        }

        hints.Children.Add(Hint("↕", _dim, Text("Overlay.Hint.Move")));
        if (model.Selected is not null)
        {
            hints.Children.Add(Hint("A", _ok, Text("Overlay.Hint.Select")));
        }

        hints.Children.Add(Hint("B", _danger, Text("Overlay.Hint.Close")));

        return new Border
        {
            Margin = new Thickness(0, 16, 0, 0),
            Background = Rgb("#0A0E15"),
            BorderBrush = _line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = hints,
        };
    }

    private void RenderConfirm(OverlayModel model)
    {
        ConfirmLayer.Visibility = model.Confirming ? Visibility.Visible : Visibility.Collapsed;
        if (!model.Confirming)
        {
            return;
        }

        var hints = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        hints.Children.Add(Hint("A", _ok, Text("Overlay.Confirm.Ok")));
        hints.Children.Add(Hint("B", _danger, Text("Overlay.Confirm.Cancel")));

        var card = new StackPanel();
        card.Children.Add(Block(Text("Overlay.Confirm.Title"), 28, _text, bold: true));
        card.Children.Add(Block(Text("Overlay.Confirm.Text"), 19, _dim, margin: new Thickness(0, 8, 0, 0)));
        card.Children.Add(hints);
        ConfirmCard.Child = card;
    }

    /// <summary>A button cap (the color of the real button) and what it does.</summary>
    private static StackPanel Hint(string cap, System.Windows.Media.Brush color, string label)
    {
        var circle = new Border
        {
            MinWidth = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = color,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = cap,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = _ground,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            },
        };

        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 22, 0) };
        row.Children.Add(circle);
        row.Children.Add(new TextBlock { Text = label, FontSize = 17, Foreground = _dim, VerticalAlignment = System.Windows.VerticalAlignment.Center });
        return row;
    }
}
