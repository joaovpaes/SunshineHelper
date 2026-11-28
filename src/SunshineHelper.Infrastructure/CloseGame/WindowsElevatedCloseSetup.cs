using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Protocol;

namespace SunshineHelper.Infrastructure.CloseGame;

/// <summary>
/// The app's side of allowing the close of games that run as administrator
/// (docs/adr/0017-fechar-jogo-de-administrador.md, section 5). The app itself never runs elevated:
/// to allow or remove, it starts the auxiliary next to it through UAC with <c>install</c> or
/// <c>uninstall</c>, and the auxiliary does the work in Program Files and the Task Scheduler.
/// </summary>
public sealed class WindowsElevatedCloseSetup
{
    private const int UacCanceled = 1223;
    private const string AdministratorsSid = "S-1-5-32-544";
    private static readonly TimeSpan _queryTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Uses the auxiliary next to the app, or <paramref name="bundledPath"/> (for tests).</summary>
    public WindowsElevatedCloseSetup(string? bundledPath = null)
    {
        BundledPath = bundledPath ?? Path.Combine(AppContext.BaseDirectory, ElevatedCloseContract.ExecutableName);
    }

    /// <summary>The auxiliary next to the app (what gets installed).</summary>
    public string BundledPath { get; }

    /// <summary>Where the auxiliary is installed, in the protected folder.</summary>
    public static string InstalledPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        ElevatedCloseContract.InstallFolderName,
        ElevatedCloseContract.ExecutableName);

    /// <summary>Looks at the PC and says where the permission stands.</summary>
    public async Task<ElevatedCloseSetupState> GetStateAsync(CancellationToken cancellationToken)
    {
        string installed = InstalledPath;
        bool bundledPresent = File.Exists(BundledPath);
        bool installedPresent = File.Exists(installed);
        bool taskRegistered = installedPresent && await IsTaskRegisteredAsync(cancellationToken).ConfigureAwait(false);
        bool sameFile = bundledPresent && installedPresent && SameContent(BundledPath, installed);

        return new ElevatedCloseSetupFacts(IsAdminAccount(), bundledPresent, installedPresent, taskRegistered, sameFile).Decide();
    }

    /// <summary>Allows (or updates): asks UAC, then the auxiliary copies itself and registers the task.</summary>
    public Task<ElevatedCloseSetupResult> InstallAsync(CancellationToken cancellationToken) =>
        RunElevatedAsync(ElevatedCloseContract.InstallVerb, cancellationToken);

    /// <summary>Removes: asks UAC, then the auxiliary deletes the task and the installed copy.</summary>
    public Task<ElevatedCloseSetupResult> UninstallAsync(CancellationToken cancellationToken) =>
        RunElevatedAsync(ElevatedCloseContract.UninstallVerb, cancellationToken);

    /// <summary>Whether the Windows account is an administrator. With UAC on, the app's token has the
    /// Administrators group only as "deny only", so that counts too.</summary>
    public static bool IsAdminAccount()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            return true;
        }

        foreach (Claim claim in identity.Claims)
        {
            if (claim.Type == ClaimTypes.DenyOnlySid && claim.Value == AdministratorsSid)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ElevatedCloseSetupResult> RunElevatedAsync(string verb, CancellationToken cancellationToken)
    {
        if (!File.Exists(BundledPath))
        {
            return new ElevatedCloseSetupResult(ElevatedCloseSetupOutcome.Failed);
        }

        var info = new ProcessStartInfo(BundledPath)
        {
            Arguments = verb,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return new ElevatedCloseSetupResult(ElevatedCloseSetupOutcome.Failed);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var code = (ElevatedCloseSetupExitCode)process.ExitCode;
            return new ElevatedCloseSetupResult(
                code == ElevatedCloseSetupExitCode.Done ? ElevatedCloseSetupOutcome.Done : ElevatedCloseSetupOutcome.Failed,
                code);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == UacCanceled)
        {
            return new ElevatedCloseSetupResult(ElevatedCloseSetupOutcome.Canceled);
        }
    }

    private static async Task<bool> IsTaskRegisteredAsync(CancellationToken cancellationToken)
    {
        string schtasks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        var info = new ProcessStartInfo(schtasks)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("/Query");
        info.ArgumentList.Add("/TN");
        info.ArgumentList.Add(ElevatedCloseContract.TaskName);

        using var process = Process.Start(info);
        if (process is null)
        {
            return false;
        }

        // Read both streams so a chatty answer can never block the process on a full pipe.
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_queryTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill();
            return false;
        }

        return process.ExitCode == 0;
    }

    private static bool SameContent(string first, string second)
    {
        try
        {
            var a = new FileInfo(first);
            var b = new FileInfo(second);
            if (a.Length != b.Length)
            {
                return false;
            }

            using FileStream left = a.OpenRead();
            using FileStream right = b.OpenRead();
            return SHA256.HashData(left).AsSpan().SequenceEqual(SHA256.HashData(right));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
