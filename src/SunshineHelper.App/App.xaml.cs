using System.Windows;
using Microsoft.Win32;
using SunshineHelper.App.Services;

// "Application" is qualified below because this project transitively references
// SunshineHelper.Application (via Infrastructure), and C# resolves an unqualified
// name against an enclosing namespace (here, a sibling "SunshineHelper.Application"
// namespace) before it considers types brought in by a `using` directive.
namespace SunshineHelper.App;

public partial class App : System.Windows.Application
{
    private ThemeManager? _theme;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _theme = new ThemeManager(new DictionarySlot(Resources));
        _theme.Apply();

        new LocalizationManager(new DictionarySlot(Resources)).Apply();

        // Windows switching between light and dark while the app is open.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A static event: left subscribed, it would keep this object alive.
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
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
