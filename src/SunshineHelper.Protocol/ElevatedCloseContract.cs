namespace SunshineHelper.Protocol;

/// <summary>
/// What the app and the auxiliary agree on for closing games that run as administrator
/// (docs/adr/0017-fechar-jogo-de-administrador.md): where the auxiliary is installed, the
/// scheduled task's name, the verbs and the exit codes of the setup modes. Shared here so both
/// sides use the same values.
/// </summary>
public static class ElevatedCloseContract
{
    /// <summary>The folder under <c>%ProgramFiles%</c> the auxiliary is copied to. Only
    /// administrators can write there, which is what makes the scheduled task safe.</summary>
    public const string InstallFolderName = "SunshineHelper";

    /// <summary>The auxiliary's file name, both next to the app and in the install folder.</summary>
    public const string ExecutableName = "SunshineHelper.Trigger.exe";

    /// <summary>The scheduled task, in the root folder of the Task Scheduler (a task in the root
    /// whose principal is the user can be started by that user without elevation).</summary>
    public const string TaskName = "SunshineHelper Close Elevated Game";

    /// <summary>Copies the auxiliary to the install folder and registers the task. Run elevated,
    /// through UAC, followed by the SID of the account that clicked "Allow".</summary>
    public const string InstallVerb = "install";

    /// <summary>Removes the task and the installed copy. Run elevated, through UAC.</summary>
    public const string UninstallVerb = "uninstall";
}

/// <summary>Exit codes of the auxiliary's <c>install</c> and <c>uninstall</c> modes.</summary>
public enum ElevatedCloseSetupExitCode
{
    /// <summary>Done.</summary>
    Done = 0,

    /// <summary>The arguments were not exactly one verb.</summary>
    BadArguments = 10,

    /// <summary>Something unexpected went wrong.</summary>
    UnexpectedError = 30,

    /// <summary>The mode was not run elevated (it must be started through UAC).</summary>
    NotElevated = 40,

    /// <summary>The auxiliary could not find its own file to copy.</summary>
    SourceMissing = 41,

    /// <summary>Copying to, or deleting from, the install folder failed (a copy may be in use).</summary>
    FileFailed = 42,

    /// <summary>The Task Scheduler refused to create or delete the task.</summary>
    TaskFailed = 43,

    /// <summary>The user account could not be read to name the task's principal.</summary>
    UserUnknown = 44,

    /// <summary>The UAC prompt was approved by another administrator account than the one that
    /// clicked "Allow"; the task would run for the wrong user, so nothing was installed.</summary>
    OtherAccount = 45,

    /// <summary>The install folder or the installed copy is a link to somewhere else, or its
    /// permissions could not be locked down to administrators.</summary>
    UnsafeFolder = 46,
}
