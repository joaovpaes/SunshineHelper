using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunshineHelper.App.Services;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Localization;
using SunshineHelper.Domain.Monitoring;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Startup;

namespace SunshineHelper.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly WindowsStartupRegistration _startup = new();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _refreshing;
    private bool _loadingGeneral;

    internal MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();

        _statusTimer.Tick += async (_, _) => await RefreshPanelAsync();
        _statusTimer.Tick += (_, _) => RefreshDevicesIfVisible();
        _statusTimer.Tick += (_, _) => RefreshLogViewIfVisible();
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
        bool isProfiles = page == "Profiles";
        ProfilesCard.Visibility = isProfiles ? Visibility.Visible : Visibility.Collapsed;
        bool isDevices = page == "Devices";
        DevicesCard.Visibility = isDevices ? Visibility.Visible : Visibility.Collapsed;
        bool isLog = page == "Log";
        LogCard.Visibility = isLog ? Visibility.Visible : Visibility.Collapsed;
        NotReadyCard.Visibility = isPanel || isGeneral || isProfiles || isDevices || isLog ? Visibility.Collapsed : Visibility.Visible;

        if (isLog)
        {
            LogViewMessage.Text = string.Empty;
            LoadLogView(force: true);
        }

        if (isDevices)
        {
            DevicesMessage.Text = string.Empty;
            LoadDevices(force: true);
        }

        if (isProfiles)
        {
            LoadProfiles(selectId: null);
        }

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
        StartWithWindowsBox.IsChecked = SafeStartupEnabled();
        LogPathBox.Text = current.SunshineLogPath ?? string.Empty;
        LauncherNamesBox.Text = string.Join(Environment.NewLine, current.Launcher.ProcessNames);
        LauncherDelayBox.Text = current.Timing.LauncherDelayMs.ToString(CultureInfo.InvariantCulture);
        FillCloseGame(current);
        AppVersionText.Text = string.Format(CultureInfo.CurrentCulture, Text("General.About.Version"), AppHost.AppVersion);
        GeneralMessage.Text = string.Empty;
        _loadingGeneral = false;
    }

    private bool SafeStartupEnabled()
    {
        try
        {
            return _startup.IsEnabled();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingGeneral)
        {
            return;
        }

        bool wanted = StartWithWindowsBox.IsChecked == true;
        try
        {
            if (wanted)
            {
                string exePath = Environment.ProcessPath
                    ?? throw new InvalidOperationException(Text("General.Startup.NoPath"));
                _startup.Enable(exePath);
            }
            else
            {
                _startup.Disable();
            }

            GeneralMessage.Text = Text(wanted ? "General.Startup.On" : "General.Startup.Off");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
        {
            // Put the box back to what is really in the registry.
            _loadingGeneral = true;
            StartWithWindowsBox.IsChecked = !wanted;
            _loadingGeneral = false;
            GeneralMessage.Text = string.Format(CultureInfo.CurrentCulture, Text("General.Startup.Failed"), ex.Message);
        }
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

    /// <summary>Puts the close-game options on screen. Callers already have
    /// <see cref="_loadingGeneral"/> set, so this does not count as the user changing them.</summary>
    private void FillCloseGame(AppSettings current)
    {
        var closeGame = current.Controller.CloseGame;
        CloseGameBox.IsChecked = closeGame.Enabled;
        CloseGameReturnBox.IsChecked = closeGame.ReturnToLauncher;
        CloseGameForceBox.IsChecked = closeGame.ForceWithoutConfirm;
        CloseGameWarning.Text = closeGame.Enabled && current.Launcher.ProcessNames.Count == 0
            ? Text("General.CloseGame.NoLauncher")
            : string.Empty;
    }

    private void FillCloseGameWarning()
    {
        var current = _host.Settings.Current;
        CloseGameWarning.Text = current.Controller.CloseGame.Enabled && current.Launcher.ProcessNames.Count == 0
            ? Text("General.CloseGame.NoLauncher")
            : string.Empty;
    }

    private async void OnCloseGameChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingGeneral)
        {
            return;
        }

        bool enabled = CloseGameBox.IsChecked == true;
        bool returnToLauncher = CloseGameReturnBox.IsChecked == true;
        bool force = CloseGameForceBox.IsChecked == true;

        bool saved = await TrySaveAsync(settings => settings with
        {
            Controller = settings.Controller with
            {
                CloseGame = settings.Controller.CloseGame with
                {
                    Enabled = enabled,
                    ReturnToLauncher = returnToLauncher,
                    ForceWithoutConfirm = force,
                },
            },
        });

        // On a failed save the boxes go back to what is really stored.
        _loadingGeneral = true;
        FillCloseGame(_host.Settings.Current);
        _loadingGeneral = false;

        if (saved)
        {
            GeneralMessage.Text = Text(enabled ? "General.CloseGame.On" : "General.CloseGame.Off");
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
            FillCloseGameWarning();
            GeneralMessage.Text = Text("General.Launcher.Saved");
        }
    }

    private void OnGenerateReport(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "*.txt|*.txt",
            DefaultExt = ".txt",
            FileName = string.Create(CultureInfo.InvariantCulture, $"sunshinehelper-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.txt"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string report = _host.BuildDiagnosticReport(ReportDescriptionBox.Text);
            File.WriteAllText(dialog.FileName, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            GeneralMessage.Text = string.Format(CultureInfo.CurrentCulture, Text("General.Report.Saved"), dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GeneralMessage.Text = string.Format(CultureInfo.CurrentCulture, Text("General.Report.Failed"), ex.Message);
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
            NotAppliedText.Text = status.NotAppliedModules.Count == 0
                ? string.Empty
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Text("Panel.NotApplied"),
                    string.Join(", ", status.NotAppliedModules.Select(id => Text("Module." + id))));
            RtssClosedText.Text = status.Kind == SessionStatusKind.Active
                && ProfileUsesFps(status.ProfileId)
                && !IsRtssRunning()
                    ? Text("Panel.RtssClosed")
                    : string.Empty;
            LogText.Text = Text(_host.LogMonitorRunning ? "Panel.Log.On" : "Panel.Log.Off");
            if (_host.LogLevel == LogLevelVerdict.Insufficient)
            {
                LogText.Text += " " + Text("Panel.Log.LowLevel");
            }

            ProblemsText.Text = _host.StartupProblems.Count == 0
                ? string.Empty
                : $"{Text("Panel.Problems")}\n{string.Join('\n', _host.StartupProblems)}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private bool ProfileUsesFps(string? profileId)
    {
        var profile = FindProfile(_host.Settings.Current, profileId);
        return profile is not null && profile.Display.FpsLimit.Mode != ProfileValueMode.Leave;
    }

    private static bool IsRtssRunning()
    {
        var processes = Process.GetProcessesByName("RTSS");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private string Text(string key) => TryFindResource(key) as string ?? key;

    /// <summary>Empty when there is no value, so an idle dashboard shows no "Device: ".</summary>
    private string Formatted(string key, string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Text(key), value);
}
