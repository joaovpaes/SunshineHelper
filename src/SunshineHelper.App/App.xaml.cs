using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using SunshineHelper.App.Services;
using SunshineHelper.Infrastructure.Startup;

// "Application" is qualified below because this project transitively references
// SunshineHelper.Application (via Infrastructure), and C# resolves an unqualified
// name against an enclosing namespace (here, a sibling "SunshineHelper.Application"
// namespace) before it considers types brought in by a `using` directive.
namespace SunshineHelper.App;

// The mutex and the tray icon live exactly as long as the process and are released in
// OnExit; an Application subclass is not something anyone disposes.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Released in OnExit.")]
public partial class App : System.Windows.Application
{
    // One copy only: a second would fail to open the pipe and fight over the snapshot.
    private const string SingleInstanceName = @"Local\SunshineHelper.SingleInstance";

    private Mutex? _singleInstance;
    private ThemeManager? _theme;
    private LocalizationManager? _localization;
    private AppHost? _host;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private OverlayHost? _overlay;

    /// <summary>True once the user chose Exit; until then closing the window only hides it.</summary>
    internal bool IsExiting { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The window is only hidden when closed; the tray's Exit is the one way out.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _theme = new ThemeManager(new DictionarySlot(Resources));
        _theme.Apply();

        _localization = new LocalizationManager(new DictionarySlot(Resources));
        _localization.Apply();

        // Windows switching between light and dark while the app is open.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _host = await AppHost.StartAsync(CancellationToken.None);
        // Settings are loaded by now: the saved language wins over Windows' own.
        ApplyLanguage(_host.Settings.Current.Language);
        _tray = new TrayIcon(ShowMainWindow, ExitApp);

        // The combo fires on a background thread; the window must open on this one.
        var overlay = new OverlayHost(_host);
        _overlay = overlay;
        _host.OverlayRequested += () => Dispatcher.InvokeAsync(overlay.Open);

        KeepStartupEntryCurrent();

        _window = new MainWindow(_host);

        // Started by Windows at sign-in (--minimized): stay in the tray. The tray's Open and
        // a double click on its icon show the window later.
        if (!WindowsStartupRegistration.IsMinimizedLaunch(e.Args))
        {
            _window.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A static event: left subscribed, it would keep this object alive.
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        _overlay?.Close();
        _tray?.Dispose();

        // Stops the pipe and the log reader. Blocking is safe: nothing in the host
        // awaits back onto this thread.
        _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();

        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>Switches the UI language live; null follows Windows.</summary>
    internal void ApplyLanguage(string? preference) => _localization?.Apply(preference);

    /// <summary>If "start with Windows" is on and the app was moved, points the entry at this copy.
    /// A failure here must never keep the app from opening.</summary>
    private static void KeepStartupEntryCurrent()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            return;
        }

        try
        {
            new WindowsStartupRegistration().RefreshIfStale(exePath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Left as it was; the Geral page shows the state and lets the user retry.
        }
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        IsExiting = true;
        Shutdown();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            // Raised on a system thread, not the UI thread.
            Dispatcher.Invoke(() => _theme?.Apply());
        }
    }
}
