using System.Reflection;
using System.Runtime.InteropServices;

namespace SunshineHelper.Tools.RtssModuleHarness;

/// <summary>
/// The RTSSHooks64.dll entry points the in-memory probe (<see cref="InMemoryProbe"/>) calls.
/// A copy of what <c>RtssHooks</c> in the app does, on purpose: that class is internal to the
/// app, and this probe must not change production code just to be reachable. Names are passed
/// as NUL-terminated ASCII bytes because the exports take LPCSTR (same reason as in the app).
/// </summary>
internal static class RtssNative
{
    public const string DllFileName = "RTSSHooks64.dll";

    private static readonly object _resolverLock = new();
    private static bool _resolverRegistered;
    private static string? _installDirectory;

    public static void EnsureResolver(string installDirectory)
    {
        _installDirectory = installDirectory;

        lock (_resolverLock)
        {
            if (_resolverRegistered)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(RtssNative).Assembly, ResolveRtssHooksDll);
            _resolverRegistered = true;
        }
    }

    private static nint ResolveRtssHooksDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, DllFileName, StringComparison.OrdinalIgnoreCase) || _installDirectory is not { } directory)
        {
            return IntPtr.Zero;
        }

        string fullPath = Path.Combine(directory, DllFileName);
        return File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out nint handle) ? handle : IntPtr.Zero;
    }

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void LoadProfile(byte[] profile);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProfileProperty(byte[] name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProfileProperty(byte[] name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void UpdateProfiles();
}
