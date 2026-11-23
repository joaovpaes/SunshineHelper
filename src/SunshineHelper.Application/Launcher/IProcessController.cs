using System.Threading;

namespace SunshineHelper.Application.Launcher;

/// <summary>One running process, identified the only way that survives PID reuse: the PID
/// together with the moment it started. Windows recycles PIDs, so a PID alone could point
/// at an unrelated program by the time the session ends.</summary>
public sealed record ProcessIdentity(int Id, string Name, DateTimeOffset StartedAt);

/// <summary>
/// The few things <see cref="LauncherModule"/> needs from Windows' process list, kept
/// behind an interface so the closing policy — the part that can do real damage if it is
/// wrong — is tested without Windows (docs/adr/0008-modulo-launcher.md).
/// </summary>
/// <remarks>
/// Every method that acts on a process must first confirm that <see cref="ProcessIdentity"/>
/// still names the same process (same PID <em>and</em> same start time) and do nothing
/// otherwise. Processes this app is not allowed to inspect are skipped, never guessed at.
/// </remarks>
public interface IProcessController
{
    /// <summary>Running processes whose name is one of <paramref name="names"/> (compared
    /// without <c>.exe</c>, ignoring case).</summary>
    IReadOnlyList<ProcessIdentity> FindByNames(IReadOnlyCollection<string> names);

    /// <summary>Asks the process to close the way the user would — by closing its main
    /// window. False when it has no window to ask, or is no longer the same process.</summary>
    bool RequestClose(ProcessIdentity process);

    /// <summary>True once the process has exited (or is no longer the same process);
    /// false if it is still running after <paramref name="timeout"/>.</summary>
    Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Ends the process and every process it started. Returns true when it is
    /// gone afterwards (or was already gone).</summary>
    Task<bool> KillTreeAsync(ProcessIdentity process, CancellationToken cancellationToken);
}
