using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Infrastructure.Launcher;

/// <summary>
/// <see cref="IProcessController"/> over <see cref="Process"/>. Every action first looks
/// the process up again and confirms it is still the same one — same PID <em>and</em> same
/// start time (within a second) — because Windows recycles PIDs: by the time a session
/// ends, the PID a launcher had may belong to something unrelated, and ending that would
/// be far worse than leaving the launcher open (docs/adr/0008-modulo-launcher.md).
/// </summary>
/// <remarks>
/// A process this app is not allowed to inspect (elevated, or owned by another user) is
/// skipped silently by <see cref="FindByNames"/> and so is never closed. The app does not
/// elevate itself to reach it.
/// </remarks>
public sealed class WindowsProcessController : IProcessController
{
    private static readonly TimeSpan _startTimeTolerance = TimeSpan.FromSeconds(1);

    /// <summary>Time given to a killed process tree to disappear before reporting it.</summary>
    private static readonly TimeSpan _killSettleTime = TimeSpan.FromSeconds(5);

    public IReadOnlyList<ProcessIdentity> FindByNames(IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var found = new List<ProcessIdentity>();
        foreach (string name in names)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (TryIdentify(process) is { } identity)
                    {
                        found.Add(identity);
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return found;
    }

    public bool RequestClose(ProcessIdentity process)
    {
        using var live = FindLive(process);
        if (live is null)
        {
            return false;
        }

        try
        {
            // False when there is no main window to ask: the caller then waits out the
            // timeout and ends the process.
            return live.CloseMainWindow();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public async Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var live = FindLive(process);
        if (live is null)
        {
            return true;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await live.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public async Task<bool> KillTreeAsync(ProcessIdentity process, CancellationToken cancellationToken)
    {
        using (var live = FindLive(process))
        {
            if (live is null)
            {
                return true;
            }

            try
            {
                live.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or AggregateException)
            {
                // Already gone, or some part of the tree is out of reach. What matters is
                // whether the process itself is still there, checked below.
            }

            using var settle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            settle.CancelAfter(_killSettleTime);
            try
            {
                await live.WaitForExitAsync(settle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Did not go away in time; reported by the lookup below.
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Same.
            }
        }

        using var stillThere = FindLive(process);
        return stillThere is null;
    }

    private static ProcessIdentity? TryIdentify(Process process)
    {
        try
        {
            return new ProcessIdentity(process.Id, process.ProcessName, new DateTimeOffset(process.StartTime));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Exited in the meantime, or not inspectable from here.
            return null;
        }
    }

    /// <summary>The running process <paramref name="expected"/> names, or null when it is
    /// gone or the PID now belongs to a different process.</summary>
    private static Process? FindLive(ProcessIdentity expected)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(expected.Id);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var actual = TryIdentify(process);
        bool same = actual is not null
            && string.Equals(actual.Name, expected.Name, StringComparison.OrdinalIgnoreCase)
            && (actual.StartedAt - expected.StartedAt).Duration() <= _startTimeTolerance;

        if (same)
        {
            return process;
        }

        process.Dispose();
        return null;
    }
}
