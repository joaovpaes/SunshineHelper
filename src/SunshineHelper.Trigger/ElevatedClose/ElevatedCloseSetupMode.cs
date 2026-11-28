using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using SunshineHelper.Protocol;

namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>Where the auxiliary lives once installed (docs/adr/0017-fechar-jogo-de-administrador.md,
/// section 5): a folder under <c>%ProgramFiles%</c>, where only administrators can write.</summary>
/// <param name="Folder">The install folder.</param>
/// <param name="Executable">The installed auxiliary.</param>
internal sealed record ElevatedCloseInstallPaths(string Folder, string Executable)
{
    /// <summary>The paths under the given Program Files folder.</summary>
    public static ElevatedCloseInstallPaths Under(string programFiles)
    {
        string folder = Path.Combine(programFiles, ElevatedCloseContract.InstallFolderName);
        return new ElevatedCloseInstallPaths(folder, Path.Combine(folder, ElevatedCloseContract.ExecutableName));
    }
}

/// <summary>
/// The <c>install</c> and <c>uninstall</c> modes of the auxiliary (ADR 0017, section 5). The app
/// starts them through UAC when the user clicks "Permitir" or "Remover"; the scheduled task can never
/// reach them, because it always passes <c>elevated-close</c> first. Install copies this executable
/// to the protected folder and registers the task; uninstall deletes both.
/// </summary>
internal static class ElevatedCloseSetupMode
{
    private const int ToolTimeoutMs = 30_000;

    private static readonly string[] _queryTask = ["/Query", "/TN", ElevatedCloseContract.TaskName];
    private static readonly string[] _deleteTask = ["/Delete", "/TN", ElevatedCloseContract.TaskName, "/F"];

    // icacls, applied to the install folder and everything in it: first the Administrators group
    // becomes the owner (an owner can always rewrite permissions, so a folder left by someone else
    // must not keep its old owner), then every permission goes back to what Program Files hands
    // down (only administrators and the system write), dropping any extra one found there.
    private static readonly string[] _ownByAdministrators = ["/setowner", "*S-1-5-32-544", "/T"];
    private static readonly string[] _resetToInherited = ["/reset", "/T"];

    /// <summary>Whether <paramref name="verb"/> selects one of these modes.</summary>
    public static bool IsSetupVerb(string verb) =>
        string.Equals(verb, ElevatedCloseContract.InstallVerb, StringComparison.Ordinal)
        || string.Equals(verb, ElevatedCloseContract.UninstallVerb, StringComparison.Ordinal);

    /// <summary>Runs <c>install</c> or <c>uninstall</c> and returns its exit code.</summary>
    public static int Run(string[] args)
    {
        try
        {
            // install <SID of the account that clicked>; uninstall alone.
            bool install = args is [ElevatedCloseContract.InstallVerb, _];
            bool uninstall = args is [ElevatedCloseContract.UninstallVerb];
            if (!install && !uninstall)
            {
                return (int)ElevatedCloseSetupExitCode.BadArguments;
            }

            if (!WindowsElevatedProcessSystem.IsCurrentProcessElevated())
            {
                return (int)ElevatedCloseSetupExitCode.NotElevated;
            }

            // Known-folder lookups, not environment variables: the user's environment could point
            // %ProgramFiles% or %SystemRoot% somewhere they can write.
            var paths = ElevatedCloseInstallPaths.Under(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            ElevatedCloseSetupExitCode result = install ? Install(paths, args[1]) : Uninstall(paths);
            return (int)result;
        }
        catch (Exception)
        {
            // Any failure means "stop here"; the exit code reports it.
            return (int)ElevatedCloseSetupExitCode.UnexpectedError;
        }
    }

    private static ElevatedCloseSetupExitCode Install(ElevatedCloseInstallPaths paths, string requestedSid)
    {
        string? source = Environment.ProcessPath;
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            return ElevatedCloseSetupExitCode.SourceMissing;
        }

        byte[]? sid = WindowsElevatedProcessSystem.CurrentUserSid();
        string? userSid = sid is null ? null : SidText.Format(sid);
        if (userSid is null)
        {
            return ElevatedCloseSetupExitCode.UserUnknown;
        }

        // The SID from the app is only compared, never used: the task always runs as the account
        // this elevated process belongs to. A different one means another administrator approved
        // the UAC prompt, and the task would be useless (and run for the wrong person).
        if (!string.Equals(requestedSid, userSid, StringComparison.OrdinalIgnoreCase))
        {
            return ElevatedCloseSetupExitCode.OtherAccount;
        }

        try
        {
            Directory.CreateDirectory(paths.Folder);

            // The whole design rests on this folder being writable by administrators only. A folder
            // that already existed (left by something else) is not trusted as found: a link anywhere
            // in it is refused (writing through one would land somewhere else), and the owner and
            // permissions are set again.
            if (IsLink(paths.Folder) || ContainsLinks(paths.Folder))
            {
                return ElevatedCloseSetupExitCode.UnsafeFolder;
            }

            string[] own = [paths.Folder, .. _ownByAdministrators];
            string[] reset = [paths.Folder, .. _resetToInherited];
            if (!RunSystemTool("icacls.exe", own) || !RunSystemTool("icacls.exe", reset))
            {
                return ElevatedCloseSetupExitCode.UnsafeFolder;
            }

            if (!SamePath(source, paths.Executable))
            {
                CopyLocked(source, paths.Executable);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ElevatedCloseSetupExitCode.FileFailed;
        }

        // The definition is written inside the protected folder, never in the user's temp folder.
        string definition = Path.Combine(paths.Folder, "task.xml");
        try
        {
            File.Delete(definition);
            File.WriteAllText(definition, ElevatedCloseTaskDefinition.Build(userSid, paths), Encoding.Unicode);
            // A task with this name made earlier by anyone else goes first, so nothing of it (its
            // permissions included) survives into the new one.
            _ = RunSystemTool("schtasks.exe", _deleteTask);
            string[] create = ["/Create", "/TN", ElevatedCloseContract.TaskName, "/XML", definition, "/F"];
            return RunSystemTool("schtasks.exe", create) ? ElevatedCloseSetupExitCode.Done : ElevatedCloseSetupExitCode.TaskFailed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ElevatedCloseSetupExitCode.FileFailed;
        }
        finally
        {
            TryDelete(definition);
        }
    }

    private static ElevatedCloseSetupExitCode Uninstall(ElevatedCloseInstallPaths paths)
    {
        // Delete, then make sure it is gone: "not found" and "deleted" both end with no task.
        _ = RunSystemTool("schtasks.exe", _deleteTask);
        if (RunSystemTool("schtasks.exe", _queryTask))
        {
            return ElevatedCloseSetupExitCode.TaskFailed;
        }

        try
        {
            // A copy running this very mode cannot delete itself; the app always starts the one
            // next to it, so this only matters if that one is missing.
            if (File.Exists(paths.Executable) && !SamePath(Environment.ProcessPath, paths.Executable))
            {
                File.Delete(paths.Executable);
            }

            if (Directory.Exists(paths.Folder) && !Directory.EnumerateFileSystemEntries(paths.Folder).Any())
            {
                Directory.Delete(paths.Folder);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ElevatedCloseSetupExitCode.FileFailed;
        }

        return ElevatedCloseSetupExitCode.Done;
    }

    /// <summary>Runs a Windows tool from System32 by its full path (never through PATH), from
    /// System32, without a window; true when it exits with 0.</summary>
    private static bool RunSystemTool(string tool, string[] arguments)
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var info = new ProcessStartInfo(Path.Combine(system, tool))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = system,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return false;
        }

        if (!process.WaitForExit(ToolTimeoutMs))
        {
            process.Kill();
            return false;
        }

        return process.ExitCode == 0;
    }

    /// <summary>Copies while holding the source open for reading only, so it cannot be renamed or
    /// changed halfway; writes next to the target first, then swaps, so a failed copy never leaves
    /// half a file.</summary>
    private static void CopyLocked(string source, string target)
    {
        string staging = target + ".new";
        File.Delete(staging);
        using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var to = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            from.CopyTo(to);
        }

        File.Move(staging, target, overwrite: true);
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool ContainsLinks(string folder)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false };
        foreach (string entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
        {
            if (IsLink(entry))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SamePath(string? first, string second) =>
        first is not null
        && string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving the definition behind is harmless: it sits in the protected folder.
        }
    }
}

/// <summary>The scheduled task's definition, in the Task Scheduler's XML format.</summary>
internal static class ElevatedCloseTaskDefinition
{
    /// <summary>
    /// A task that runs as <paramref name="userSid"/>, only while that user is signed in (no stored
    /// password), with the highest privileges the account has; hidden, with no trigger (started on
    /// demand only), stopped after 30 seconds, and never twice at once. Its single action is the
    /// installed auxiliary with <c>elevated-close</c> fixed in front of the three values the app
    /// passes (ADR 0017, section 2).
    /// </summary>
    public static string Build(string userSid, ElevatedCloseInstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string user = SecurityElement.Escape(userSid);
        string command = SecurityElement.Escape("\"" + paths.Executable + "\"");
        string folder = SecurityElement.Escape(paths.Folder);
        string name = SecurityElement.Escape("\\" + ElevatedCloseContract.TaskName);
        string arguments = ElevatedCloseRequest.Verb + " $(Arg0) $(Arg1) $(Arg2)";

        return $$"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>SunshineHelper</Author>
                <Description>SunshineHelper: closes a game that runs as administrator when you hold the close-game combo during a stream. Remove it from SunshineHelper's General screen.</Description>
                <URI>{{name}}</URI>
              </RegistrationInfo>
              <Principals>
                <Principal id="Author">
                  <UserId>{{user}}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>true</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT30S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{{command}}</Command>
                  <Arguments>{{arguments}}</Arguments>
                  <WorkingDirectory>{{folder}}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}

/// <summary>Writes a binary SID in its usual text form (<c>S-1-5-21-...</c>).</summary>
internal static class SidText
{
    /// <summary>The text form of <paramref name="sid"/>; null when the bytes are not a valid SID.</summary>
    public static string? Format(ReadOnlySpan<byte> sid)
    {
        if (sid.Length < 8)
        {
            return null;
        }

        int revision = sid[0];
        int count = sid[1];
        if (revision != 1 || count > 15 || sid.Length != 8 + (4 * count))
        {
            return null;
        }

        ulong authority = 0;
        for (int index = 2; index < 8; index++)
        {
            authority = (authority << 8) | sid[index];
        }

        var text = new StringBuilder("S-1-");
        text.Append(authority.ToString(CultureInfo.InvariantCulture));
        for (int index = 0; index < count; index++)
        {
            uint part = BinaryPrimitives.ReadUInt32LittleEndian(sid.Slice(8 + (4 * index), 4));
            text.Append('-').Append(part.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }
}
