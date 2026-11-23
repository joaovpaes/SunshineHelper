using System.IO;
using Microsoft.Win32;

namespace SunshineHelper.App.Services;

/// <summary>
/// Dark or light, following Windows (docs/adr/0005-casca-da-interface.md). The overlay
/// will always be dark; that is its own window's business, not this class's.
/// </summary>
internal sealed class ThemeManager(DictionarySlot slot)
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public void Apply()
    {
        string name = WindowsUsesLightTheme() ? "Light" : "Dark";
        slot.Set(new Uri($"pack://application:,,,/SunshineHelper;component/Themes/Colors.{name}.xaml"));
    }

    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Dark is the design's default, and a theme that cannot be read is not worth
            // failing the window over.
            return false;
        }
    }
}
