using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Win32;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Infrastructure.Fps;

/// <summary>
/// Caps the frame rate by writing RTSS's "FramerateLimit" profile property, through the
/// same <c>RTSSHooks64.dll</c> entry points RTSS's own UI and third-party tools use to
/// change a profile live (no documented public API exists for this either, like HDR —
/// see ADR 0002). Every call targets the empty profile name, which is RTSS's own
/// convention for "the global profile" (applies to every process, not a specific game).
/// </summary>
/// <remarks>
/// <para>
/// <b>RTSS is optional and user-installed</b> — unlike the display or HDR module, not
/// having it is a normal, expected state, not a failure: <see cref="CaptureAsync"/>
/// records that plainly instead of throwing, so a PC without RTSS can still stream with
/// the resolution and HDR modules working. Only <i>asking</i> for an FPS limit on a PC
/// without RTSS is a failure, because there would be nothing to honor it with.
/// </para>
/// <para>
/// The profile lives in a file on disk, so unlike <see cref="Display.PrimaryDisplayModule"/>
/// and <see cref="Display.HdrModule"/>, <see cref="SurvivesReboot"/> is <c>true</c>: a
/// reboot does not put it back by itself, so a crash-recovery reboot still needs this
/// module reverted (docs/adr/0002-arquitetura-do-nucleo.md, "Boot diferente").
/// </para>
/// <para>
/// Finding RTSS's own install folder never assumes the author's own PC: it checks, in
/// order, the running <c>RTSS</c> process (if one is already up), then the uninstall
/// registry entry RTSS's own installer writes, then the common default path as a last
/// resort. Nothing about a specific machine is hard-coded.
/// </para>
/// </remarks>
public sealed class RtssFpsLimitModule : IEnvironmentModule
{
    private const int CurrentPayloadVersion = 1;
    private const string DefaultInstallDirectory = @"C:\Program Files (x86)\RivaTuner Statistics Server";

    // NUL-terminated ASCII, because RTSS's exports take LPCSTR (see RtssHooks). The empty
    // name is RTSS's own convention for "the global profile" — the one that applies to
    // every process, rather than a specific game's profile.
    private static readonly byte[] _globalProfileName = "\0"u8.ToArray();
    private static readonly byte[] _framerateLimitProperty = "FramerateLimit\0"u8.ToArray();

    public string Id => "fps";

    public int Order => 3; // After the display (1) and HDR (2) modules.

    public bool SurvivesReboot => true;

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        string? directory = FindInstallDirectory();
        if (directory is null)
        {
            return Task.FromResult(NotInstalledSnapshot());
        }

        RtssHooks.EnsureResolver(directory);
        int current = ReadFramerateLimit();

        var payload = new JsonObject { ["installed"] = true, ["framerateLimit"] = current };
        return Task.FromResult(new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload });
    }

    public Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        if (desired.FpsLimit is not { } target)
        {
            return Task.FromResult(ApplyOutcome.NothingToDo());
        }

        string? directory = FindInstallDirectory();
        if (directory is null)
        {
            return Task.FromResult(ApplyOutcome.Failed("O RTSS não parece estar instalado; não foi possível aplicar o limite de FPS."));
        }

        RtssHooks.EnsureResolver(directory);
        return Task.FromResult(SetFramerateLimit(target));
    }

    public Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.PayloadVersion != CurrentPayloadVersion)
        {
            return Task.FromResult(RevertOutcome.Failed(
                $"O snapshot do módulo de FPS foi gravado numa versão ({snapshot.PayloadVersion}) "
                + $"que esta versão do app não lê (esperado {CurrentPayloadVersion})."));
        }

        if (snapshot.Payload is not { } payload)
        {
            return Task.FromResult(RevertOutcome.Failed("O snapshot do módulo de FPS não tem dados."));
        }

        bool wasInstalled = payload["installed"]!.GetValue<bool>();
        if (!wasInstalled)
        {
            // Nothing was captured, because RTSS wasn't there at the time — nothing to
            // put back either.
            return Task.FromResult(RevertOutcome.Success());
        }

        int previous = payload["framerateLimit"]!.GetValue<int>();

        string? directory = FindInstallDirectory();
        if (directory is null)
        {
            return Task.FromResult(RevertOutcome.Failed(
                "O RTSS estava instalado quando a sessão começou e não está mais; não foi possível restaurar o limite de FPS."));
        }

        RtssHooks.EnsureResolver(directory);
        var outcome = SetFramerateLimit(previous);
        return Task.FromResult(outcome.Status == ApplyStatus.Failed
            ? RevertOutcome.Failed(outcome.ErrorMessage ?? "Falha desconhecida ao restaurar o limite de FPS.")
            : RevertOutcome.Success());
    }

    private static ModuleSnapshot NotInstalledSnapshot()
    {
        var payload = new JsonObject { ["installed"] = false };
        return new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload };
    }

    private static ApplyOutcome SetFramerateLimit(int value)
    {
        if (!WriteFramerateLimit(value))
        {
            return ApplyOutcome.Failed("O RTSS recusou o novo limite de FPS (SetProfileProperty).");
        }

        int confirmed = ReadFramerateLimit();
        if (confirmed != value)
        {
            return ApplyOutcome.Failed(
                $"O RTSS relatou sucesso, mas o limite lido de volta é {confirmed}, não {value}.");
        }

        return ApplyOutcome.Changed();
    }

    private static int ReadFramerateLimit()
    {
        RtssHooks.LoadProfile(_globalProfileName);

        nint data = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            if (!RtssHooks.GetProfileProperty(_framerateLimitProperty, data, sizeof(int)))
            {
                throw new InvalidOperationException("Não foi possível ler o limite de FPS do RTSS (GetProfileProperty).");
            }

            return Marshal.ReadInt32(data);
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static bool WriteFramerateLimit(int value)
    {
        RtssHooks.LoadProfile(_globalProfileName);

        nint data = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(data, value);
            if (!RtssHooks.SetProfileProperty(_framerateLimitProperty, data, sizeof(int)))
            {
                return false;
            }

            // RTSS does not necessarily copy the 4 bytes out the instant
            // SetProfileProperty returns — SaveProfile/UpdateProfiles may still read
            // through this same pointer to actually commit the change. Freeing it any
            // earlier than this handed RTSS a dangling pointer: SetProfileProperty kept
            // reporting success, but the value that landed was whatever was left in that
            // freed memory, which is exactly the bug a manual test caught (reported
            // "sucesso", limite não mudou).
            RtssHooks.SaveProfile(_globalProfileName);
            RtssHooks.UpdateProfiles();
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static string? FindInstallDirectory()
    {
        return TryGetDirectoryFromRunningProcess()
            ?? TryGetDirectoryFromRegistry()
            ?? TryGetDefaultDirectory();
    }

    private static string? TryGetDirectoryFromRunningProcess()
    {
        foreach (var process in Process.GetProcessesByName("RTSS"))
        {
            using (process)
            {
                try
                {
                    string? exePath = process.MainModule?.FileName;
                    if (exePath is not null)
                    {
                        return Path.GetDirectoryName(exePath);
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // MainModule can refuse access (different elevation level, process
                    // exiting right now); the registry and the default path are still
                    // there to try.
                }
            }
        }

        return null;
    }

    private static string? TryGetDirectoryFromRegistry()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            string? candidate = TryReadInstallLocation(view);
            if (candidate is not null && HasHooksDll(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryReadInstallLocation(RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RTSS");
            if (key is null)
            {
                return null;
            }

            if (key.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            // Not every installer fills in InstallLocation; the uninstaller or its icon
            // sits in the same folder as RTSS.exe either way.
            string? exeLikePath = key.GetValue("UninstallString") as string ?? key.GetValue("DisplayIcon") as string;
            return exeLikePath is not null ? Path.GetDirectoryName(exeLikePath) : null;
        }
        catch (SecurityException)
        {
            return null;
        }
    }

    private static string? TryGetDefaultDirectory()
        => HasHooksDll(DefaultInstallDirectory) ? DefaultInstallDirectory : null;

    private static bool HasHooksDll(string directory)
        => File.Exists(Path.Combine(directory, RtssHooks.DllFileName));
}
