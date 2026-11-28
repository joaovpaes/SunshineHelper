namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>
/// The exit code of the <c>elevated-close</c> mode (docs/adr/0017-fechar-jogo-de-administrador.md). The
/// mode writes nothing to disk, so this number, read by the app from the scheduled task's last result,
/// is the only way it says what happened. Unlike <c>start</c> and <c>end</c>, this mode is never run by
/// Sunshine, so a non-zero exit cannot abort a stream.
/// </summary>
internal enum ElevatedCloseExitCode
{
    /// <summary>The close was asked for (<c>close</c>) or the process was ended (<c>kill</c>).</summary>
    Done = 0,

    /// <summary>The arguments were not exactly <c>elevated-close</c>, an operation and two numbers.</summary>
    BadArguments = 10,

    /// <summary>No process with that id and that exact start time: it is gone, or the id now
    /// belongs to another process.</summary>
    NotFound = 11,

    /// <summary>Windows would not let the mode look at the process.</summary>
    NotInspectable = 12,

    /// <summary>A Windows, Sunshine or SunshineHelper process, by name.</summary>
    ForbiddenName = 13,

    /// <summary>A process Windows marks as critical.</summary>
    Critical = 14,

    /// <summary>The process is not elevated: the app can close it on its own.</summary>
    NotElevated = 15,

    /// <summary>The process belongs to another user or another Windows session.</summary>
    OtherUser = 16,

    /// <summary>No running <c>sunshine.exe</c> among the process's ancestors.</summary>
    NotStartedBySunshine = 17,

    /// <summary>The process has no visible top-level window.</summary>
    NoVisibleWindow = 18,

    /// <summary>Every rule passed, but Windows refused the close or the kill.</summary>
    ActionFailed = 20,

    /// <summary>Something unexpected went wrong; nothing more was done.</summary>
    UnexpectedError = 30,
}
