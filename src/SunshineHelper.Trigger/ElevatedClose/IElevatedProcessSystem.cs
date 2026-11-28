namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>A process as the ancestry walk sees it.</summary>
/// <param name="ProcessId">Its id.</param>
/// <param name="ParentProcessId">The id of the process that started it (0 when unknown). Windows does
/// not update it when the parent exits, so the id may since belong to another process; the walk checks
/// start times for that.</param>
/// <param name="Name">The executable's file name without the folder or <c>.exe</c>.</param>
/// <param name="StartTime">Creation time as a Windows FILETIME (UTC).</param>
/// <param name="RunsAsSystemOrElevated">Whether it runs as LocalSystem or with an elevated token. Only
/// filled for ancestors (<see cref="IElevatedProcessSystem.Describe"/>): a <c>sunshine.exe</c> counts
/// only when true, so a copy dropped and started by an ordinary program does not.</param>
internal sealed record ProcessNode(int ProcessId, int ParentProcessId, string Name, long StartTime, bool RunsAsSystemOrElevated = false);

/// <summary>What happened when opening a process.</summary>
internal enum OpenStatus
{
    /// <summary>Opened; the process is held until disposed, so its id cannot be reused meanwhile.</summary>
    Opened,

    /// <summary>No such process.</summary>
    NotFound,

    /// <summary>Windows refused access.</summary>
    Denied,
}

/// <summary>
/// The Windows side of the <c>elevated-close</c> mode, behind an interface so the rules in
/// <see cref="ElevatedCloseRules"/> can be tested with made-up processes. Every answer that cannot be
/// read leans to the safe side (not elevated, not the caller's, critical, no window), so a failure to
/// read turns into a refusal.
/// </summary>
internal interface IElevatedProcessSystem
{
    /// <summary>Opens the process the request names; <paramref name="forKill"/> also asks for the right
    /// to end it.</summary>
    OpenStatus Open(int processId, bool forKill, out ITargetProcess? process);

    /// <summary>Name, parent and start time of another process, for the ancestry walk; null when it
    /// is gone or cannot be read.</summary>
    ProcessNode? Describe(int processId);
}

/// <summary>An opened target. All checks and the action itself go through the same open process, so
/// the process checked is the process acted on.</summary>
internal interface ITargetProcess : IDisposable
{
    /// <summary>Name, parent and start time.</summary>
    ProcessNode Node { get; }

    /// <summary>Whether its token is elevated.</summary>
    bool IsElevated { get; }

    /// <summary>Whether it runs as the same user, in the same Windows session, as this process.</summary>
    bool BelongsToCaller { get; }

    /// <summary>Whether Windows marks it as critical (ending it would stop the system).</summary>
    bool IsCritical { get; }

    /// <summary>Whether it owns a visible, unowned top-level window.</summary>
    bool HasVisibleWindow { get; }

    /// <summary>Posts <c>WM_CLOSE</c> to its visible top-level windows; false when none took it.</summary>
    bool RequestClose();

    /// <summary>Ends this process only and waits briefly for it to go; false when it did not.</summary>
    bool Kill();
}
