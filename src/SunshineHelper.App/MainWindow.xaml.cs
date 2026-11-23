using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunshineHelper.App.Services;
using SunshineHelper.Application.Session;

namespace SunshineHelper.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _refreshing;

    internal MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();

        _statusTimer.Tick += async (_, _) => await RefreshPanelAsync();
        _statusTimer.Start();

        // Not set in XAML: SelectionChanged would fire before PageTitle exists.
        NavList.SelectedIndex = 0;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // The app lives in the tray: the X only hides the window. Exit is in the tray menu.
        if (System.Windows.Application.Current is App { IsExiting: false })
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private async void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem { Tag: string page })
        {
            return;
        }

        // SetResourceReference, not a copied string: the text keeps following the language
        // if it is switched while the window is open.
        PageTitle.SetResourceReference(TextBlock.TextProperty, $"Page.{page}.Title");
        PageDescription.SetResourceReference(TextBlock.TextProperty, $"Page.{page}.Description");

        bool isPanel = page == "Panel";
        PanelCard.Visibility = isPanel ? Visibility.Visible : Visibility.Collapsed;
        NotReadyCard.Visibility = isPanel ? Visibility.Collapsed : Visibility.Visible;

        if (isPanel)
        {
            await RefreshPanelAsync();
        }
    }

    private async Task RefreshPanelAsync()
    {
        // A slow disk must not stack up ticks, and a hidden window has nobody to show it to.
        if (_refreshing || !IsVisible || PanelCard.Visibility != Visibility.Visible)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var status = await _host.Status.ReadAsync(CancellationToken.None);

            StatusText.Text = Text(status.Kind switch
            {
                SessionStatusKind.Active => "Panel.Status.Active",
                SessionStatusKind.NeedsAttention => "Panel.Status.NeedsAttention",
                _ => "Panel.Status.Idle",
            });

            ClientText.Text = Formatted("Panel.Device", status.ClientName);
            ProfileText.Text = Formatted("Panel.Profile", status.ProfileId);
            LogText.Text = Text(_host.LogMonitorRunning ? "Panel.Log.On" : "Panel.Log.Off");

            ProblemsText.Text = _host.StartupProblems.Count == 0
                ? string.Empty
                : $"{Text("Panel.Problems")}\n{string.Join('\n', _host.StartupProblems)}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private string Text(string key) => TryFindResource(key) as string ?? key;

    /// <summary>Empty when there is no value, so an idle dashboard shows no "Device: ".</summary>
    private string Formatted(string key, string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Text(key), value);
}
