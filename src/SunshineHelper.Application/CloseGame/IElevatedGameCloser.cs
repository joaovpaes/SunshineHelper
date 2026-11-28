using System.Threading;
using SunshineHelper.Application.Launcher;

namespace SunshineHelper.Application.CloseGame;

/// <summary>What the scheduled task is asked to do with a game that runs as administrator.</summary>
public enum ElevatedCloseOperation
{
    /// <summary>Ask the game's window to close, like clicking its X.</summary>
    Close,

    /// <summary>End the game (the game alone, not the programs it started).</summary>
    Kill,
}

/// <summary>How a request to the scheduled task went, as far as the app can tell.</summary>
public enum ElevatedCloseStatus
{
    /// <summary>The permission is not there: the task is not registered (the user never
    /// clicked "Allow", or removed it).</summary>
    NotAllowed,

    /// <summary>The task ran and finished; <see cref="ElevatedCloseAttempt.ExitCode"/> says
    /// what it did.</summary>
    Finished,

    /// <summary>The task was started, but its answer was not read in time. Whether the game is
    /// gone is still checked on the process itself.</summary>
    Unknown,

    /// <summary>The Task Scheduler refused to start the task.</summary>
    Failed,
}

/// <summary>The result of one request to the scheduled task.</summary>
/// <param name="Status">How it went.</param>
/// <param name="ExitCode">With <see cref="ElevatedCloseStatus.Finished"/>, the exit code of the
/// auxiliary's <c>elevated-close</c> mode (<see cref="ElevatedCloseReason"/>), or a Windows code
/// when the task itself could not run.</param>
public sealed record ElevatedCloseAttempt(ElevatedCloseStatus Status, int? ExitCode = null);

/// <summary>
/// Closes or ends a game that runs as administrator through the scheduled task the user allowed
/// on the General screen (docs/adr/0017-fechar-jogo-de-administrador.md, sections 2 and 6). The
/// app never runs elevated: it only starts the task with the operation, the PID and the start
/// time, and the auxiliary checks every rule again on its own before acting.
/// </summary>
public interface IElevatedGameCloser
{
    /// <summary>Starts the task for <paramref name="process"/> and waits for its answer.</summary>
    Task<ElevatedCloseAttempt> RunAsync(ElevatedCloseOperation operation, ProcessIdentity process, CancellationToken cancellationToken);

    /// <summary>Waits until <paramref name="process"/> (same PID and start time) is gone, for at
    /// most <paramref name="timeout"/>. Works for a process this app cannot open. True when it
    /// is gone.</summary>
    Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Used when no closer is given: the permission is never there.</summary>
internal sealed class NoElevatedGameCloser : IElevatedGameCloser
{
    public static NoElevatedGameCloser Instance { get; } = new();

    public Task<ElevatedCloseAttempt> RunAsync(ElevatedCloseOperation operation, ProcessIdentity process, CancellationToken cancellationToken) =>
        Task.FromResult(new ElevatedCloseAttempt(ElevatedCloseStatus.NotAllowed));

    public Task<bool> WaitForExitAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
