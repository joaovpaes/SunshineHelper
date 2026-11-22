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

    // Every name below is passed as NUL-terminated bytes, not as a C# string, because
    // RTSS's exports take LPCSTR. Marshaling a string to LPCSTR would run it through the
    // machine's current ANSI code page, where a character that has no representation is
    // silently replaced by a look-alike (what CA2101 is about); the DllImport flags that
    // are supposed to control that are ignored on modern .NET, so they would quiet the
    // analyzer without changing anything. Bytes remove the conversion altogether — the
    // names we pass are ASCII constants (see RtssFpsLimitModule), so there is nothing to
    // convert in the first place. Do not "simplify" these parameters back to string.

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void LoadProfile(byte[] profile);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void SaveProfile(byte[] profile);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProfileProperty(byte[] name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProfileProperty(byte[] name, nint data, uint size);

    [DllImport(DllFileName, CallingConvention = CallingConvention.StdCall)]
    public static extern void UpdateProfiles();
}
