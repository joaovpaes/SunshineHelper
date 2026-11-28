using System.Runtime.InteropServices;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Infrastructure.CloseGame;

/// <summary>One process as the system's process list describes it.</summary>
/// <param name="Id">The process id.</param>
/// <param name="ImageName">The file name, with its extension (<c>mkw.exe</c>).</param>
/// <param name="CreateTime">When it started, as a FILETIME (100 ns since 1601, UTC).</param>
internal readonly record struct SystemProcess(int Id, string ImageName, long CreateTime)
{
    /// <summary>The name the way .NET's <c>Process.ProcessName</c> gives it (no <c>.exe</c>), so the
    /// app's protections compare it like every other name.</summary>
    public string Name => ImageName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        ? ImageName[..^4]
        : ImageName;

    public ProcessIdentity ToIdentity() => new(Id, Name, DateTimeOffset.FromFileTime(CreateTime));
}

/// <summary>
/// Reads the system's process list (<c>NtQuerySystemInformation</c>), which gives the id, the
/// name and the start time of every process without opening any of them. That is how
/// the app identifies a game that runs as administrator: Windows does not let a program that is not
/// elevated open such a process, so .NET cannot read its start time
/// (docs/adr/0017-fechar-jogo-de-administrador.md).
/// </summary>
/// <remarks>The layout read here is <c>SYSTEM_PROCESS_INFORMATION</c> on 64-bit Windows, the only
/// platform the app ships for; on anything else the list is empty and the app behaves as before.</remarks>
internal static partial class SystemProcessList
{
    private const int SystemProcessInformation = 5;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int MaxAttempts = 5;

    // Offsets in SYSTEM_PROCESS_INFORMATION (x64).
    private const int NextEntryOffset = 0x00;
    private const int CreateTimeOffset = 0x20;
    private const int ImageNameLengthOffset = 0x38;
    private const int ImageNameBufferOffset = 0x40;
    private const int ProcessIdOffset = 0x50;

    /// <summary>Every process right now, or an empty list when the read fails.</summary>
    public static IReadOnlyList<SystemProcess> Read()
    {
        if (!Environment.Is64BitProcess)
        {
            return [];
        }

        int size = 512 * 1024;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            nint buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQuerySystemInformation(SystemProcessInformation, buffer, (uint)size, out uint needed);
                if (status == StatusInfoLengthMismatch)
                {
                    // The list grows between the two calls; leave room for that.
                    size = (int)Math.Max(needed, (uint)size) + (64 * 1024);
                    continue;
                }

                return status < 0 ? [] : Parse(buffer, size);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return [];
    }

    /// <summary>The process with <paramref name="id"/>, or null when there is none.</summary>
    public static SystemProcess? Find(int id) => Find(Read(), id);

    /// <summary>The process with <paramref name="id"/> in <paramref name="list"/>, or null.</summary>
    public static SystemProcess? Find(IReadOnlyList<SystemProcess> list, int id)
    {
        foreach (SystemProcess process in list)
        {
            if (process.Id == id)
            {
                return process;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="expected"/> (same id, name and start time) is still in
    /// <paramref name="list"/>.</summary>
    public static bool Contains(IReadOnlyList<SystemProcess> list, ProcessIdentity expected) =>
        Find(list, expected.Id) is { } found && Matches(found, expected);

    /// <summary>Whether <paramref name="found"/> is the process <paramref name="expected"/> names.
    /// The start time is compared exactly: both come from the same FILETIME.</summary>
    public static bool Matches(SystemProcess found, ProcessIdentity expected) =>
        found.Id == expected.Id
        && found.CreateTime == expected.StartedAt.ToFileTime()
        && string.Equals(found.Name, expected.Name, StringComparison.OrdinalIgnoreCase);

    private static List<SystemProcess> Parse(nint buffer, int size)
    {
        var list = new List<SystemProcess>();
        int offset = 0;
        while (offset >= 0 && offset + ProcessIdOffset + 8 <= size)
        {
            nint entry = buffer + offset;
            long id = Marshal.ReadInt64(entry, ProcessIdOffset);
            long createTime = Marshal.ReadInt64(entry, CreateTimeOffset);
            int nameBytes = (ushort)Marshal.ReadInt16(entry, ImageNameLengthOffset);
            nint nameBuffer = Marshal.ReadIntPtr(entry, ImageNameBufferOffset);
            string name = nameBuffer == 0 || nameBytes == 0
                ? string.Empty
                : Marshal.PtrToStringUni(nameBuffer, nameBytes / 2);

            if (id is > 0 and <= int.MaxValue)
            {
                list.Add(new SystemProcess((int)id, name, createTime));
            }

            int next = Marshal.ReadInt32(entry, NextEntryOffset);
            if (next <= 0)
            {
                break;
            }

            offset += next;
        }

        return list;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtQuerySystemInformation(int informationClass, nint information, uint length, out uint returnLength);
}
