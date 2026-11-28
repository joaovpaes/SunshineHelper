using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Protocol;

namespace SunshineHelper.Infrastructure.CloseGame;

/// <summary>
/// <see cref="IElevatedGameCloser"/> over the Task Scheduler (docs/adr/0017-fechar-jogo-de-administrador.md,
/// section 2). It starts the task the user allowed on the General screen with <c>RunEx</c>, passing
/// only the operation, the PID and the start time, then reads the task's last result for the log.
/// <c>schtasks /Run</c> cannot pass values to a task, so the Task Scheduler's own COM service is used,
/// late bound (it is a scripting interface, and the app is not trimmed).
/// </summary>
public sealed class WindowsElevatedGameCloser : IElevatedGameCloser
{
    private const int TaskStateRunning = 4;
    private const int TaskStillRunning = 0x00041301;
    private const int TaskNotYetRun = 0x00041303;
    private const int FileNotFound = unchecked((int)0x80070002);
    private const int PathNotFound = unchecked((int)0x80070003);

    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the app waits for the task's answer. The auxiliary is quick: a close is
    /// one message, and a kill waits at most 5 s for the game to go.</summary>
    private static readonly TimeSpan _answerTimeout = TimeSpan.FromSeconds(12);

    private static readonly TimeSpan _exitPoll = TimeSpan.FromMilliseconds(250);

    public async Task<ElevatedCloseAttempt> RunAsync(ElevatedCloseOperation operation, ProcessIdentity process, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        string[] values =
        [
            operation == ElevatedCloseOperation.Kill ? "kill" : "close",
            process.Id.ToString(CultureInfo.InvariantCulture),
            process.StartedAt.ToFileTime().ToString(CultureInfo.InvariantCulture),
        ];

        dynamic? service = null;
        try
        {
            service = CreateService();
            if (service is null)
            {
                return new ElevatedCloseAttempt(ElevatedCloseStatus.Failed);
            }

            dynamic? task = GetTask((object)service);
            if (task is null)
            {
                return new ElevatedCloseAttempt(ElevatedCloseStatus.NotAllowed);
            }

            DateTime lastRunBefore = (DateTime)task.LastRunTime;

            // Flags 0 and no user: the task runs as registered (the user, elevated).
            task.RunEx(values, 0, 0, string.Empty);

            DateTimeOffset deadline = DateTimeOffset.UtcNow + _answerTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(_poll, cancellationToken).ConfigureAwait(false);

                // Read again each time, so the state is the scheduler's, not a cached copy.
                dynamic? current = GetTask((object)service);
                if (current is null)
                {
                    return new ElevatedCloseAttempt(ElevatedCloseStatus.Unknown);
                }

                int state = (int)current.State;
                DateTime lastRun = (DateTime)current.LastRunTime;
                int result = (int)current.LastTaskResult;
                if (state != TaskStateRunning && lastRun != lastRunBefore && result is not (TaskStillRunning or TaskNotYetRun))
                {
                    return new ElevatedCloseAttempt(ElevatedCloseStatus.Finished, result);
                }
            }

            return new ElevatedCloseAttempt(ElevatedCloseStatus.Unknown);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The late binder turns a COM error into the matching .NET exception
            // (FileNotFoundException, UnauthorizedAccessException, COMException...); any of
            // them means the task could not be used, never a crash of the round.
            return new ElevatedCloseAttempt(ElevatedCloseStatus.Failed);
        }
        finally
        {
            if (service is not null)
            {
                Marshal.FinalReleaseComObject((object)service);
            }
        }
    }

    public async Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            if (!SystemProcessList.Contains(SystemProcessList.Read(), process))
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(_exitPoll, cancellationToken).ConfigureAwait(false);
        }
    }

    private static dynamic? CreateService()
    {
        Type? type = Type.GetTypeFromProgID("Schedule.Service");
        if (type is null)
        {
            return null;
        }

        dynamic? service = Activator.CreateInstance(type);
        service?.Connect();
        return service;
    }

    /// <summary>The registered task, or null when it is not there.</summary>
    private static dynamic? GetTask(object service)
    {
        try
        {
            dynamic folder = ((dynamic)service).GetFolder("\\");
            return folder.GetTask(ElevatedCloseContract.TaskName);
        }
        catch (Exception ex) when (ex.HResult is FileNotFound or PathNotFound)
        {
            // The binder raises FileNotFoundException (or a COMException) for a missing task.
            return null;
        }
    }
}
