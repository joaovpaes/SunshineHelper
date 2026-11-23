using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Security.Principal;

namespace SunshineHelper.Infrastructure.Fps;

/// <summary>What <see cref="RtssProfileAccess.Check"/> found.</summary>
public enum RtssProfileAccessStatus
{
    /// <summary>RTSS is not installed: nothing to grant, an FPS limit simply cannot apply.</summary>
    NotInstalled,

    /// <summary>The global profile file is missing (RTSS never saved it yet).</summary>
    ProfileMissing,

    /// <summary>This account can write the global profile: the FPS module works without admin.</summary>
    Granted,

    /// <summary>RTSS is there but this account cannot write its global profile.</summary>
    Missing,
}

/// <summary>What <see cref="RtssProfileAccess.RequestGrant"/> ended with.</summary>
public enum RtssGrantOutcome
{
    Granted,
    Cancelled,
    Failed,
}

/// <summary>Outcome plus a message in Portuguese for the user (null on success).</summary>
public sealed record RtssGrantResult(RtssGrantOutcome Outcome, string? Message);

/// <summary>
/// Whether this account can write RTSS's global profile file, and a way to grant that once
/// (docs/adr/0012-permissao-do-perfil-do-rtss.md). The app itself never runs elevated: the
/// only elevated step is Windows' own <c>icacls.exe</c>, started through UAC, granting the
/// current user's SID read and write on that single file — never on the RTSS folder, where
/// write access would let anyone swap the DLL RTSS injects into games.
/// </summary>
public static class RtssProfileAccess
{
    /// <summary>How long to wait for icacls after the user accepts the UAC prompt.</summary>
    private const int GrantTimeoutMs = 60_000;

    /// <summary>ERROR_CANCELLED: the user said no on the UAC prompt.</summary>
    private const int ErrorCancelled = 1223;

    public static RtssProfileAccessStatus Check()
    {
        string? directory = RtssInstallLocator.FindInstallDirectory();
        if (directory is null)
        {
            return RtssProfileAccessStatus.NotInstalled;
        }

        string profile = GlobalProfilePath(directory);
        if (!File.Exists(profile))
        {
            return RtssProfileAccessStatus.ProfileMissing;
        }

        return CanWrite(profile) ? RtssProfileAccessStatus.Granted : RtssProfileAccessStatus.Missing;
    }

    /// <summary>The file RTSS's <c>SaveProfile</c> writes for the global (empty-name) profile.</summary>
    public static string GlobalProfilePath(string installDirectory)
        => Path.Combine(installDirectory, "Profiles", "Global");

    /// <summary>
    /// True when <c>SaveProfile</c> for the global profile should succeed. With the profile
    /// file present, only that file matters (that is what gets granted). Without it, RTSS
    /// would have to create it, which needs write access to the folder: probed with a
    /// throwaway file, as before ADR 0012.
    /// </summary>
    public static bool CanSaveGlobalProfile(string installDirectory)
    {
        string profile = GlobalProfilePath(installDirectory);
        return File.Exists(profile) ? CanWrite(profile) : CanCreateFileIn(Path.GetDirectoryName(profile)!);
    }

    /// <summary>
    /// Opens the file for writing and closes it without writing anything: the contents and
    /// the modification time stay as they were. False when Windows refuses.
    /// </summary>
    public static bool CanWrite(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Asks Windows (UAC) to run icacls once and grant this account read and write on the
    /// global profile file. Returns <see cref="RtssGrantOutcome.Granted"/> only after
    /// <see cref="CanWrite"/> confirms it.
    /// </summary>
    public static RtssGrantResult RequestGrant()
    {
        string? directory = RtssInstallLocator.FindInstallDirectory();
        if (directory is null)
        {
            return new RtssGrantResult(RtssGrantOutcome.Failed, "O RTSS não parece estar instalado.");
        }

        string profile = GlobalProfilePath(directory);
        if (!File.Exists(profile))
        {
            return new RtssGrantResult(
                RtssGrantOutcome.Failed,
                "O RTSS ainda não criou o perfil global. Abra o RTSS uma vez, mude qualquer opção e tente de novo.");
        }

        if (CanWrite(profile))
        {
            return new RtssGrantResult(RtssGrantOutcome.Granted, null);
        }

        using var identity = WindowsIdentity.GetCurrent();
        string? sid = identity.User?.Value;
        if (sid is null || BuildIcaclsArguments(profile, sid) is not { } arguments)
        {
            return new RtssGrantResult(RtssGrantOutcome.Failed, "Não foi possível montar o pedido de permissão para este caminho ou usuário.");
        }

        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe"), arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new RtssGrantResult(RtssGrantOutcome.Failed, "O Windows não iniciou o icacls.");
            }

            if (!process.WaitForExit(GrantTimeoutMs))
            {
                return new RtssGrantResult(RtssGrantOutcome.Failed, "O icacls não terminou a tempo.");
            }

            if (process.ExitCode != 0)
            {
                string code = process.ExitCode.ToString(CultureInfo.InvariantCulture);
                return new RtssGrantResult(RtssGrantOutcome.Failed, "O icacls terminou com erro (código " + code + ").");
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new RtssGrantResult(RtssGrantOutcome.Cancelled, "Pedido de permissão cancelado na janela do Windows.");
        }
        catch (Win32Exception ex)
        {
            return new RtssGrantResult(RtssGrantOutcome.Failed, "O Windows recusou iniciar o icacls: " + ex.Message);
        }

        return CanWrite(profile)
            ? new RtssGrantResult(RtssGrantOutcome.Granted, null)
            : new RtssGrantResult(RtssGrantOutcome.Failed, "O icacls terminou, mas o perfil do RTSS continua sem permissão de escrita.");
    }

    /// <summary>
    /// The icacls command line, or null when the path or SID looks wrong. Only a fully
    /// qualified path to a file named <c>Global</c> inside a <c>Profiles</c> folder is
    /// accepted, without quotes, and the SID must look like one: whatever ends up on this
    /// line runs elevated, so nothing unexpected gets through.
    /// </summary>
    public static string? BuildIcaclsArguments(string profilePath, string sid)
    {
        if (!IsAcceptableProfilePath(profilePath) || !IsAcceptableSid(sid))
        {
            return null;
        }

        // "*" tells icacls the account is a SID, so a localized or renamed user name never
        // matters. (R,W) is read and write on this one file; nothing is inherited or
        // granted on the folder.
        return "\"" + profilePath + "\" /grant *" + sid + ":(R,W)";
    }

    public static bool IsAcceptableProfilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('"', StringComparison.Ordinal) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        string? folder = Path.GetDirectoryName(path);
        return string.Equals(Path.GetFileName(path), "Global", StringComparison.OrdinalIgnoreCase)
            && folder is not null
            && string.Equals(Path.GetFileName(folder), "Profiles", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAcceptableSid(string sid)
    {
        if (string.IsNullOrEmpty(sid) || !sid.StartsWith("S-1-", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char c in sid)
        {
            if (!char.IsAsciiDigit(c) && c != '-' && c != 'S')
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanCreateFileIn(string directory)
    {
        string probePath = Path.Combine(directory, $".sunshinehelper-write-check-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(probePath, []);
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return false;
        }
    }
}
