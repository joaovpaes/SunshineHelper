using Microsoft.Win32;

namespace SunshineHelper.Infrastructure.Startup;

/// <summary>
/// Starts the app when the user signs in to Windows. It writes one value under the
/// current user's Run key: no administrator rights, no scheduled task, and Windows
/// Settings (Apps, Startup) shows and can switch it off too. The registry is the single
/// source of truth; nothing about this lives in settings.json.
/// </summary>
public sealed class WindowsStartupRegistration
{
    /// <summary>Added to the Run command so a sign-in start opens straight into the tray.
    /// A start by hand (no argument) still shows the window.</summary>
    public const string MinimizedArgument = "--minimized";

    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string DefaultValueName = "SunshineHelper";

    private readonly string _keyPath;
    private readonly string _valueName;

    public WindowsStartupRegistration()
        : this(DefaultKeyPath, DefaultValueName)
    {
    }

    /// <summary>Takes the location so tests can use a throwaway key instead of the real Run key.</summary>
    public WindowsStartupRegistration(string keyPath, string valueName)
    {
        _keyPath = keyPath;
        _valueName = valueName;
    }

    /// <summary>The path is quoted because "C:\Program Files\..." has spaces.</summary>
    public static string BuildCommand(string exePath) =>
        string.Concat("\"", exePath, "\" ", MinimizedArgument);

    public static bool IsMinimizedLaunch(IEnumerable<string> arguments)
    {
        foreach (string argument in arguments)
        {
            if (string.Equals(argument, MinimizedArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsEnabled() => ReadCommand() is not null;

    public void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue(_valueName, BuildCommand(exePath), RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    /// <summary>If it is on but points at an old place (the user moved the folder), points
    /// it at the running copy. Returns true when it rewrote the value. Never turns it on.</summary>
    public bool RefreshIfStale(string exePath)
    {
        string? current = ReadCommand();
        if (current is null || string.Equals(current, BuildCommand(exePath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Enable(exePath);
        return true;
    }

    private string? ReadCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(_valueName) as string;
    }
}
