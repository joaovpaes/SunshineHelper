using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunshineHelper.App.Services;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Localization;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _refreshing;
    private bool _loadingGeneral;

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
        bool isGeneral = page == "General";
        GeneralCard.Visibility = isGeneral ? Visibility.Visible : Visibility.Collapsed;
        NotReadyCard.Visibility = isPanel || isGeneral ? Visibility.Collapsed : Visibility.Visible;

        if (isGeneral)
        {
            LoadGeneral();
        }

        if (isPanel)
        {
            await RefreshPanelAsync();
        }
    }

    private void LoadGeneral()
    {
        // Filling the controls must not count as the user changing them.
        _loadingGeneral = true;
        var current = _host.Settings.Current;
        LanguageBox.SelectedIndex = current.Language switch
        {
            LanguageResolver.Portuguese => 1,
            LanguageResolver.English => 2,
            _ => 0,
        };
        LogPathBox.Text = current.SunshineLogPath ?? string.Empty;
        LauncherNamesBox.Text = string.Join(Environment.NewLine, current.Launcher.ProcessNames);
        LauncherDelayBox.Text = current.Timing.LauncherDelayMs.ToString(CultureInfo.InvariantCulture);
        GeneralMessage.Text = string.Empty;
        _loadingGeneral = false;
    }

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingGeneral || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        string? language = tag.Length == 0 ? null : tag;
        if (await TrySaveAsync(settings => settings with { Language = language }))
        {
            ((App)System.Windows.Application.Current).ApplyLanguage(language);
            GeneralMessage.Text = string.Empty;
        }
    }

    private void OnBrowseLog(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "sunshine.log|*.log|*.*|*.*",
            CheckFileExists = false,
        };

        if (dialog.ShowDialog(this) == true)
        {
            LogPathBox.Text = dialog.FileName;
        }
    }

    private async void OnSaveLogPath(object sender, RoutedEventArgs e)
    {
        var check = LogPathRule.Check(LogPathBox.Text);
        if (!check.IsValid)
        {
            GeneralMessage.Text = Text("General.PathInvalid");
            return;
        }

        if (await TrySaveAsync(settings => settings with { SunshineLogPath = check.Path }))
        {
            LogPathBox.Text = check.Path ?? string.Empty;
            GeneralMessage.Text = Text(check.Path is null ? "General.SavedOff" : "General.Saved");
        }
    }

    private async void OnSaveLauncher(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LauncherDelayBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int delayMs)
            || delayMs > LauncherSettings.MaxDelayMs)
        {
            GeneralMessage.Text = Text("General.Launcher.DelayInvalid");
            return;
        }

        var names = LauncherSettings.ParseNames(LauncherNamesBox.Text);
        bool saved = await TrySaveAsync(settings => settings with
        {
            Launcher = settings.Launcher with { ProcessNames = names },
            Timing = settings.Timing with { LauncherDelayMs = delayMs },
        });

        if (saved)
        {
            LauncherNamesBox.Text = string.Join(Environment.NewLine, names);
            GeneralMessage.Text = Text("General.Launcher.Saved");
        }
    }

    /// <summary>Saves through the host; a disk problem is shown on screen, never thrown
    /// out of an event handler (which would close the app).</summary>
    private async Task<bool> TrySaveAsync(Func<AppSettings, AppSettings> change)
    {
        try
        {
            await _host.UpdateSettingsAsync(change, CancellationToken.None);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            GeneralMessage.Text = string.Format(CultureInfo.CurrentCulture, Text("General.SaveFailed"), ex.Message);
            return false;
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
