using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace SunshineHelper.Infrastructure.Fps;

/// <summary>
/// Finds RTSS's install folder without assuming the author's own PC: the running
/// <c>RTSS</c> process first, then the uninstall registry entry RTSS's own installer writes,
/// then the common default path as a last resort. Shared by <see cref="RtssFpsLimitModule"/>
/// and <see cref="RtssProfileAccess"/>, so both always talk about the same folder.
/// </summary>
internal static class RtssInstallLocator
{
    private const string DefaultInstallDirectory = @"C:\Program Files (x86)\RivaTuner Statistics Server";

    private static readonly RegistryView[] _registryViews = [RegistryView.Registry64, RegistryView.Registry32];

    public static string? FindInstallDirectory()
    {
        return TryGetDirectoryFromRunningProcess()
            ?? TryGetDirectoryFromRegistry()
            ?? TryGetDefaultDirectory();
    }

    private static string? TryGetDirectoryFromRunningProcess()
    {
        foreach (var process in Process.GetProcessesByName("RTSS"))
        {
            using (process)
            {
                try
                {
                    string? exePath = process.MainModule?.FileName;
                    if (exePath is not null)
                    {
                        return Path.GetDirectoryName(exePath);
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // MainModule can refuse access (different elevation level, process
                    // exiting right now); the registry and the default path are still
                    // there to try.
                }
            }
        }

        return null;
    }

    private static string? TryGetDirectoryFromRegistry()
    {
        foreach (var view in _registryViews)
        {
            string? candidate = TryReadInstallLocation(view);
            if (candidate is not null && HasHooksDll(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryReadInstallLocation(RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RTSS");
            if (key is null)
            {
                return null;
            }

            if (key.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            // Not every installer fills in InstallLocation; the uninstaller or its icon
            // sits in the same folder as RTSS.exe either way.
            string? exeLikePath = key.GetValue("UninstallString") as string ?? key.GetValue("DisplayIcon") as string;
            return exeLikePath is not null ? Path.GetDirectoryName(exeLikePath) : null;
        }
        catch (SecurityException)
        {
            return null;
        }
    }

    private static string? TryGetDefaultDirectory()
        => HasHooksDll(DefaultInstallDirectory) ? DefaultInstallDirectory : null;

    private static bool HasHooksDll(string directory)
        => File.Exists(Path.Combine(directory, RtssHooks.DllFileName));
}
