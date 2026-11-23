using System.Diagnostics;
using SunshineHelper.Application.Monitoring;

namespace SunshineHelper.Infrastructure.Monitoring;

/// <summary>
/// Lists the running <c>sunshine.exe</c> processes by id. Only reads the system's process
/// list: Sunshine runs as SYSTEM and is never opened, so no elevation is needed and
/// nothing about the process is touched (docs/adr/0013-sunshine-encerrado-restaura.md).
/// </summary>
public sealed class SunshineProcessProbe : ISunshineProcessProbe
{
    /// <summary>Sunshine's own executable name (not something of the author's PC).
    /// <c>sunshinesvc.exe</c> is only the service wrapper that starts it.</summary>
    public const string ProcessName = "sunshine";

    public IReadOnlyCollection<int> FindProcessIds()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        var ids = new List<int>(processes.Length);
        foreach (var process in processes)
        {
            ids.Add(process.Id);
            process.Dispose();
        }

        return ids;
    }
}
