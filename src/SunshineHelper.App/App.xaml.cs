using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using SunshineHelper.App.Services;

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
    private AppHost? _host;
    private TrayIcon? _tray;
    private MainWindow? _window;

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

        new LocalizationManager(new DictionarySlot(Resources)).Apply();

        // Windows switching between light and dark while the app is open.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _host = await AppHost.StartAsync(CancellationToken.None);
        _tray = new TrayIcon(ShowMainWindow, ExitApp);

        _window = new MainWindow(_host);
        _window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A static event: left subscribed, it would keep this object alive.
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

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
