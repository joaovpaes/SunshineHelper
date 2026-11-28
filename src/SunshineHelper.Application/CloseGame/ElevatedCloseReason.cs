using System.Globalization;

namespace SunshineHelper.Application.CloseGame;

/// <summary>
/// The exit codes of the auxiliary's <c>elevated-close</c> mode, as the app reads them from the
/// scheduled task's last result (docs/adr/0017-fechar-jogo-de-administrador.md, section 3). The
/// auxiliary keeps its own copy (it references neither Application nor Infrastructure); a test in
/// <c>SunshineHelper.Trigger.Tests</c> keeps both lists equal.
/// </summary>
public enum ElevatedCloseReason
{
    Done = 0,
    BadArguments = 10,
    NotFound = 11,
    NotInspectable = 12,
    ForbiddenName = 13,
    Critical = 14,
    NotElevated = 15,
    OtherUser = 16,
    NotStartedBySunshine = 17,
    NoVisibleWindow = 18,
    ActionFailed = 20,
    UnexpectedError = 30,
}

/// <summary>The log text for an exit code of the scheduled task.</summary>
public static class ElevatedCloseReasons
{
    public static string Describe(int exitCode) => (ElevatedCloseReason)exitCode switch
    {
        ElevatedCloseReason.Done => "done",
        ElevatedCloseReason.BadArguments => "the request was malformed",
        ElevatedCloseReason.NotFound => "the process is gone, or its id now belongs to another process",
        ElevatedCloseReason.NotInspectable => "Windows did not let it look at the process",
        ElevatedCloseReason.ForbiddenName => "it is a Windows, Sunshine or SunshineHelper program",
        ElevatedCloseReason.Critical => "Windows marks the process as critical",
        ElevatedCloseReason.NotElevated => "the process does not run as administrator",
        ElevatedCloseReason.OtherUser => "the process belongs to another user or session",
        ElevatedCloseReason.NotStartedBySunshine => "the process was not started by Sunshine",
        ElevatedCloseReason.NoVisibleWindow => "the process has no visible window",
        ElevatedCloseReason.ActionFailed => "Windows refused the close or the kill",
        ElevatedCloseReason.UnexpectedError => "something unexpected went wrong in the auxiliary",
        _ => Unknown(exitCode),
    };

    private static string Unknown(int exitCode)
    {
        string hex = exitCode.ToString("X8", CultureInfo.InvariantCulture);
        return "the task did not run as expected (result 0x" + hex + ")";
    }
}
