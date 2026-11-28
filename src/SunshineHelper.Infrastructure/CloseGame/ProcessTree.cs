using System.Runtime.InteropServices;

namespace SunshineHelper.Infrastructure.CloseGame;

/// <summary>
/// Who started whom, read from a Toolhelp snapshot of the process list. .NET has no call for
/// a process's parent, and the close-game action needs it to see whether the game started a
/// program that must not be ended with it (docs/adr/0016-fechar-jogo-pelo-controle.md).
/// </summary>
internal static partial class ProcessTree
{
    private const uint SnapProcess = 0x00000002;
    private const nint InvalidHandle = -1;

    /// <summary>The PIDs of every process started by <paramref name="rootId"/>, children and
    /// their children (the root itself is not included). Empty when the snapshot fails.</summary>
    public static IReadOnlyList<int> DescendantsOf(int rootId)
    {
        var childrenOf = new Dictionary<int, List<int>>();
        nint snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle || snapshot == 0)
        {
            return [];
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            bool more = Process32FirstW(snapshot, ref entry);
            while (more)
            {
                int parent = (int)entry.ParentProcessId;
                if (!childrenOf.TryGetValue(parent, out List<int>? list))
                {
                    list = [];
                    childrenOf[parent] = list;
                }

                list.Add((int)entry.ProcessId);
                more = Process32NextW(snapshot, ref entry);
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var result = new List<int>();
        var seen = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!childrenOf.TryGetValue(current, out List<int>? children))
            {
                continue;
            }

            foreach (int child in children)
            {
                // The seen set also stops a loop made by a recycled PID.
                if (seen.Add(child))
                {
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(nint snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(nint snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    /// <summary>PROCESSENTRY32W. Only the process and parent ids are read; the rest keeps
    /// the layout right.</summary>
#pragma warning disable CS0649 // Fields the native call fills in; only some are read.
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ProcessEntry
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
