namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>
/// Entry point of the <c>elevated-close</c> mode (docs/adr/0017-fechar-jogo-de-administrador.md). It
/// runs elevated, started by a scheduled task, and does one thing to one process, then exits. It does
/// not talk to the pipe, read settings, write files or start anything.
/// </summary>
internal static class ElevatedCloseMode
{
    /// <summary>Runs the mode and returns its exit code (see <see cref="ElevatedCloseExitCode"/>).</summary>
    public static int Run(string[] args)
    {
        try
        {
            if (!ElevatedCloseRequest.TryParse(args, out ElevatedCloseRequest? request))
            {
                return (int)ElevatedCloseExitCode.BadArguments;
            }

            return (int)ElevatedCloseRules.Run(request, new WindowsElevatedProcessSystem());
        }
        catch (Exception)
        {
            // Any failure means "do nothing more"; the exit code reports it.
            return (int)ElevatedCloseExitCode.UnexpectedError;
        }
    }
}
