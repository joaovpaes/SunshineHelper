using System.Reflection;
using System.Runtime.InteropServices;

namespace SunshineHelper.Infrastructure.Fps;

/// <summary>
/// The five <c>RTSSHooks64.dll</c> entry points <see cref="RtssFpsLimitModule"/> needs,
/// and the plumbing to load that DLL from wherever RTSS is actually installed.
/// </summary>
/// <remarks>
/// <c>[DllImport]</c> can only name a DLL by a compile-time constant, and RTSS's install
/// folder is neither fixed nor known at compile time, so plain <c>DllImport</c> alone
/// cannot find it. <see cref="EnsureResolver"/> registers a
/// <see cref="NativeLibrary.SetDllImportResolver"/> callback instead, which the runtime
/// asks, the first time any method below is actually called, to resolve the plain
/// "RTSSHooks64.dll" name — and that callback knows the real folder. Returning
/// <see cref="IntPtr.Zero"/> from the callback (done here whenever the folder is not
/// known yet, or does not have the DLL) falls back to the normal search rules, which is
/// only ever reached if <see cref="EnsureResolver"/> was never called at all.
/// </remarks>
internal static class RtssHooks
{
    public const string DllFileName = "RTSSHooks64.dll";

    private static readonly object _resolverLock = new();
    private static bool _resolverRegistered;
    private static string? _installDirectory;

    /// <summary>Points future native calls at <paramref name="installDirectory"/>. Safe to
    /// call repeatedly (every module method call does) — only the first call actually
    /// registers anything.</summary>
    public static void EnsureResolver(string installDirectory)
    {
        _installDirectory = installDirectory;

        if (_resolverRegistered)
        {
            return;
        }

        lock (_resolverLock)
        {
            if (_resolverRegistered)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(RtssHooks).Assembly, ResolveRtssHooksDll);
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

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern void LoadProfile(string profile);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern void SaveProfile(string profile);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProfileProperty(string name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProfileProperty(string name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void UpdateProfiles();
}
