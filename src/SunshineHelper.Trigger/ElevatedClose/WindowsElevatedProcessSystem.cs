using System.Runtime.InteropServices;

// This code can run elevated (ADR 0017): Windows DLLs are only ever loaded from System32, never from
// the current folder or PATH.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>
/// <see cref="IElevatedProcessSystem"/> on Win32, with hand-written <c>LibraryImport</c> calls (no
/// reflection, NativeAOT-safe). Every check on the target and the action itself use one process handle,
/// opened first: while it is open Windows cannot give the id to another process, so the process checked
/// is the process acted on. Whatever cannot be read answers on the safe side.
/// </summary>
internal sealed partial class WindowsElevatedProcessSystem : IElevatedProcessSystem
{
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x0010_0000;
    private const int ErrorInvalidParameter = 87;

    /// <summary>Whether this process runs elevated (the setup modes must be started through UAC).</summary>
    public static bool IsCurrentProcessElevated() => NativeMethods.IsElevated(NativeMethods.CurrentProcess);

    /// <summary>The SID of the user this process runs as, as bytes; null when unreadable.</summary>
    public static byte[]? CurrentUserSid() => NativeMethods.UserSid(NativeMethods.CurrentProcess);

    /// <inheritdoc />
    public OpenStatus Open(int processId, bool forKill, out ITargetProcess? process)
    {
        process = null;
        uint access = ProcessQueryLimitedInformation | Synchronize | (forKill ? ProcessTerminate : 0u);
        nint handle = NativeMethods.OpenProcess(access, 0, (uint)processId);
        if (handle == 0)
        {
            return Marshal.GetLastPInvokeError() == ErrorInvalidParameter ? OpenStatus.NotFound : OpenStatus.Denied;
        }

        // The snapshot is taken only now, with the target held open, so the parent read for this id
        // belongs to this process and not to an earlier one that had the same id.
        int parentId = NativeMethods.ParentOf(processId);
        string? name = NativeMethods.ImageName(handle);
        long? startTime = NativeMethods.StartTime(handle);
        if (name is null || startTime is null)
        {
            _ = NativeMethods.CloseHandle(handle);
            return OpenStatus.Denied;
        }

        process = new WindowsTargetProcess(handle, new ProcessNode(processId, parentId, name, startTime.Value));
        return OpenStatus.Opened;
    }

    /// <inheritdoc />
    public ProcessNode? Describe(int processId)
    {
        nint handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, 0, (uint)processId);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            int parentId = NativeMethods.ParentOf(processId);
            string? name = NativeMethods.ImageName(handle);
            long? startTime = NativeMethods.StartTime(handle);
            if (name is null || startTime is null)
            {
                return null;
            }

            bool trusted = NativeMethods.IsElevated(handle) || NativeMethods.IsLocalSystem(handle);
            return new ProcessNode(processId, parentId, name, startTime.Value, trusted);
        }
        finally
        {
            _ = NativeMethods.CloseHandle(handle);
        }
    }

    private sealed class WindowsTargetProcess(nint handle, ProcessNode node) : ITargetProcess
    {
        private const uint KillExitCode = 1;
        private const uint KillWaitMs = 5_000;
        private const uint WaitObject0 = 0;

        private nint _handle = handle;

        public ProcessNode Node { get; } = node;

        public bool IsElevated => NativeMethods.IsElevated(_handle);

        public bool BelongsToCaller => NativeMethods.SameUserAndSession(_handle, Node.ProcessId);

        public bool IsCritical => NativeMethods.IsCritical(_handle);

        public bool HasVisibleWindow => NativeMethods.TopLevelWindowsOf(Node.ProcessId).Count > 0;

        public bool RequestClose()
        {
            bool posted = false;
            foreach (nint window in NativeMethods.TopLevelWindowsOf(Node.ProcessId))
            {
                posted |= NativeMethods.PostClose(window);
            }

            return posted;
        }

        public bool Kill() =>
            NativeMethods.TerminateProcess(_handle, KillExitCode) != 0
            && NativeMethods.WaitForSingleObject(_handle, KillWaitMs) == WaitObject0;

        public void Dispose()
        {
            nint toClose = Interlocked.Exchange(ref _handle, 0);
            if (toClose != 0)
            {
                _ = NativeMethods.CloseHandle(toClose);
            }
        }
    }

    /// <summary>The Win32 calls, kept together. BOOL results are read as <c>int</c> (non-zero is
    /// true) so no marshalling is needed.</summary>
    private static unsafe partial class NativeMethods
    {
        private const uint SnapProcess = 0x0000_0002;
        private const nint InvalidHandle = -1;
        public const nint CurrentProcess = -1;
        private const uint TokenQuery = 0x0008;
        private const int TokenUserClass = 1;
        private const int TokenElevationClass = 20;
        private const uint GwOwner = 4;
        private const uint WmClose = 0x0010;
        private const int MaxPath = 32_768;

        /// <summary>S-1-5-18 (LocalSystem), the account Sunshine's service runs <c>sunshine.exe</c> as.</summary>
        private static readonly byte[] _localSystemSid = [1, 1, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0];

        /// <summary>The parent id the process list shows for <paramref name="processId"/>; 0 when it is
        /// not in the list.</summary>
        public static int ParentOf(int processId)
        {
            nint snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
            if (snapshot == InvalidHandle || snapshot == 0)
            {
                return 0;
            }

            try
            {
                var entry = new ProcessEntry { Size = (uint)sizeof(ProcessEntry) };
                bool more = Process32FirstW(snapshot, &entry) != 0;
                while (more)
                {
                    if (entry.ProcessId == (uint)processId)
                    {
                        return (int)entry.ParentProcessId;
                    }

                    more = Process32NextW(snapshot, &entry) != 0;
                }

                return 0;
            }
            finally
            {
                _ = CloseHandle(snapshot);
            }
        }

        /// <summary>The executable's file name without folder or extension; null when unreadable.</summary>
        public static string? ImageName(nint process)
        {
            char[] buffer = new char[MaxPath];
            uint size = (uint)buffer.Length;
            fixed (char* text = buffer)
            {
                if (QueryFullProcessImageNameW(process, 0, text, &size) == 0 || size == 0)
                {
                    return null;
                }
            }

            string path = new(buffer, 0, (int)size);
            return Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>Creation time as a FILETIME (UTC); null when unreadable.</summary>
        public static long? StartTime(nint process)
        {
            long creation;
            long exit;
            long kernel;
            long user;
            return GetProcessTimes(process, &creation, &exit, &kernel, &user) != 0 && creation > 0 ? creation : null;
        }

        public static bool IsElevated(nint process)
        {
            nint token;
            if (OpenProcessToken(process, TokenQuery, &token) == 0)
            {
                return false;
            }

            try
            {
                int elevation = 0;
                uint length;
                return GetTokenInformation(token, TokenElevationClass, &elevation, sizeof(int), &length) != 0 && elevation != 0;
            }
            finally
            {
                _ = CloseHandle(token);
            }
        }

        public static bool IsLocalSystem(nint process)
        {
            byte[]? sid = UserSid(process);
            return sid is not null && sid.AsSpan().SequenceEqual(_localSystemSid);
        }

        public static bool SameUserAndSession(nint process, int processId)
        {
            uint targetSession;
            uint ownSession;
            if (ProcessIdToSessionId((uint)processId, &targetSession) == 0
                || ProcessIdToSessionId((uint)Environment.ProcessId, &ownSession) == 0
                || targetSession != ownSession)
            {
                return false;
            }

            byte[]? target = UserSid(process);
            byte[]? own = UserSid(CurrentProcess);
            return target is not null && own is not null && target.AsSpan().SequenceEqual(own);
        }

        public static bool IsCritical(nint process)
        {
            int critical;
            // Unreadable counts as critical: the safe answer is to refuse.
            return IsProcessCritical(process, &critical) == 0 || critical != 0;
        }

        /// <summary>The visible top-level windows that belong to <paramref name="processId"/> and have
        /// no owner (dialogs owned by another window are left out).</summary>
        public static List<nint> TopLevelWindowsOf(int processId)
        {
            var search = new WindowSearch((uint)processId);
            GCHandle state = GCHandle.Alloc(search);
            try
            {
                _ = EnumWindows(&CollectWindow, GCHandle.ToIntPtr(state));
            }
            finally
            {
                state.Free();
            }

            return search.Windows;
        }

        public static bool PostClose(nint window) => PostMessageW(window, WmClose, 0, 0) != 0;

        /// <summary>The SID of the user a process runs as, as bytes; null when unreadable.</summary>
        public static byte[]? UserSid(nint process)
        {
            nint token;
            if (OpenProcessToken(process, TokenQuery, &token) == 0)
            {
                return null;
            }

            try
            {
                uint needed;
                _ = GetTokenInformation(token, TokenUserClass, null, 0, &needed);
                if (needed == 0 || needed > 4096)
                {
                    return null;
                }

                byte[] buffer = new byte[needed];
                fixed (byte* data = buffer)
                {
                    if (GetTokenInformation(token, TokenUserClass, data, needed, &needed) == 0)
                    {
                        return null;
                    }

                    // TOKEN_USER starts with SID_AND_ATTRIBUTES, whose first field points at the SID.
                    nint sid = *(nint*)data;
                    if (sid == 0 || IsValidSid(sid) == 0)
                    {
                        return null;
                    }

                    uint length = GetLengthSid(sid);
                    return new ReadOnlySpan<byte>((void*)sid, (int)length).ToArray();
                }
            }
            finally
            {
                _ = CloseHandle(token);
            }
        }

        [UnmanagedCallersOnly]
        private static int CollectWindow(nint window, nint state)
        {
            if (GCHandle.FromIntPtr(state).Target is not WindowSearch search)
            {
                return 0;
            }

            uint owner;
            _ = GetWindowThreadProcessId(window, &owner);
            if (owner == search.ProcessId && IsWindowVisible(window) != 0 && GetWindow(window, GwOwner) == 0)
            {
                search.Windows.Add(window);
            }

            return 1;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial nint OpenProcess(uint access, int inheritHandle, uint processId);

        [LibraryImport("kernel32.dll")]
        public static partial int CloseHandle(nint handle);

        [LibraryImport("kernel32.dll")]
        public static partial int TerminateProcess(nint process, uint exitCode);

        [LibraryImport("kernel32.dll")]
        public static partial uint WaitForSingleObject(nint handle, uint milliseconds);

        [LibraryImport("kernel32.dll")]
        private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

        [LibraryImport("kernel32.dll")]
        private static partial int Process32FirstW(nint snapshot, ProcessEntry* entry);

        [LibraryImport("kernel32.dll")]
        private static partial int Process32NextW(nint snapshot, ProcessEntry* entry);

        [LibraryImport("kernel32.dll")]
        private static partial int QueryFullProcessImageNameW(nint process, uint flags, char* name, uint* size);

        [LibraryImport("kernel32.dll")]
        private static partial int GetProcessTimes(nint process, long* creation, long* exit, long* kernel, long* user);

        [LibraryImport("kernel32.dll")]
        private static partial int ProcessIdToSessionId(uint processId, uint* sessionId);

        [LibraryImport("kernel32.dll")]
        private static partial int IsProcessCritical(nint process, int* critical);

        [LibraryImport("advapi32.dll")]
        private static partial int OpenProcessToken(nint process, uint access, nint* token);

        [LibraryImport("advapi32.dll")]
        private static partial int GetTokenInformation(nint token, int informationClass, void* information, uint length, uint* returnLength);

        [LibraryImport("advapi32.dll")]
        private static partial int IsValidSid(nint sid);

        [LibraryImport("advapi32.dll")]
        private static partial uint GetLengthSid(nint sid);

        [LibraryImport("user32.dll")]
        private static partial int EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint state);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(nint window, uint* processId);

        [LibraryImport("user32.dll")]
        private static partial int IsWindowVisible(nint window);

        [LibraryImport("user32.dll")]
        private static partial nint GetWindow(nint window, uint command);

        [LibraryImport("user32.dll")]
        private static partial int PostMessageW(nint window, uint message, nuint wParam, nint lParam);

        private sealed class WindowSearch(uint processId)
        {
            public uint ProcessId { get; } = processId;

            public List<nint> Windows { get; } = [];
        }

        /// <summary>PROCESSENTRY32W. Only the ids are read; the rest keeps the layout right.</summary>
#pragma warning disable CS0649 // Fields the native call fills in; only some are read.
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessEntry
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public nuint DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            public fixed char ExeFile[260];
        }
#pragma warning restore CS0649
    }
}
