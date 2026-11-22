using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace SunshineHelper.Infrastructure.Display;

/// <summary>
/// Changes the resolution of the primary display, keeping its current refresh rate — the
/// app never touches that (see the project's README). Scoped to the primary display for
/// v1, matching the single-monitor setup Phase 0 was tested on
/// (docs/fase0-resultados.md, "Teste 4"); picking the display by the <c>device_id</c>
/// Sunshine reports is future work for a multi-monitor setup.
/// </summary>
/// <remarks>
/// <para>
/// Every mode change goes through <c>ChangeDisplaySettingsEx</c> with <c>dwFlags</c> left
/// at zero: an in-session change only, never written to the registry as the user's own
/// default. The whole point of this module is to put the mode back later, and persisting
/// the streaming resolution as the default would defeat that.
/// </para>
/// <para>
/// Confirmation of the applied mode comes only from this module's own call to
/// <c>EnumDisplaySettingsEx</c>, right after changing it — never from an external process.
/// Phase 0 found that an external tool's own readback (<c>QRes /S</c>) can report the
/// previous mode even though the change actually took effect
/// (docs/fase0-resultados.md, "Teste rápido do 4K").
/// </para>
/// </remarks>
public sealed class PrimaryDisplayModule : IEnvironmentModule
{
    private const int CurrentPayloadVersion = 1;

    public string Id => "display";

    public int Order => 1;

    // Windows restores the user's own mode by itself; this module's state does not
    // survive a reboot.
    public bool SurvivesReboot => false;

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var current = QueryCurrentMode();

        var payload = new JsonObject
        {
            ["width"] = (int)current.dmPelsWidth,
            ["height"] = (int)current.dmPelsHeight,
            ["refreshRateHz"] = (int)current.dmDisplayFrequency,
        };

        return Task.FromResult(new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload });
    }

    public Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        if (desired.Resolution is not { } target)
        {
            return Task.FromResult(ApplyOutcome.NothingToDo());
        }

        // The refresh rate always comes from what the display is running right now, never
        // from the profile: this module has no setting of its own for it.
        var current = QueryCurrentMode();
        return Task.FromResult(ChangeMode(target.Width, target.Height, (int)current.dmDisplayFrequency));
    }

    public Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.PayloadVersion != CurrentPayloadVersion)
        {
            return Task.FromResult(RevertOutcome.Failed(
                $"O snapshot do módulo de vídeo foi gravado numa versão ({snapshot.PayloadVersion}) "
                + $"que esta versão do app não lê (esperado {CurrentPayloadVersion})."));
        }

        if (snapshot.Payload is not { } payload)
        {
            return Task.FromResult(RevertOutcome.Failed("O snapshot do módulo de vídeo não tem dados."));
        }

        int width = payload["width"]!.GetValue<int>();
        int height = payload["height"]!.GetValue<int>();
        int refreshRateHz = payload["refreshRateHz"]!.GetValue<int>();

        var outcome = ChangeMode(width, height, refreshRateHz);
        return Task.FromResult(outcome.Status == ApplyStatus.Failed
            ? RevertOutcome.Failed(outcome.ErrorMessage ?? "Falha desconhecida ao restaurar o vídeo.")
            : RevertOutcome.Success());
    }

    private static DEVMODEW QueryCurrentMode()
    {
        var mode = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };

        if (!PInvoke.EnumDisplaySettingsEx(
            null,
            ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS,
            ref mode,
            0))
        {
            throw new InvalidOperationException("Não foi possível ler o modo de vídeo atual (EnumDisplaySettingsEx).");
        }

        return mode;
    }

    private static ApplyOutcome ChangeMode(int width, int height, int refreshRateHz)
    {
        var mode = new DEVMODEW
        {
            dmSize = (ushort)Marshal.SizeOf<DEVMODEW>(),
            dmFields = DEVMODE_FIELD_FLAGS.DM_PELSWIDTH
                | DEVMODE_FIELD_FLAGS.DM_PELSHEIGHT
                | DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY,
            dmPelsWidth = (uint)width,
            dmPelsHeight = (uint)height,
            dmDisplayFrequency = (uint)refreshRateHz,
        };

        var result = PInvoke.ChangeDisplaySettingsEx(
            default,
            in mode,
            HWND.Null,
            (CDS_TYPE)0,
            null);

        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
        {
            return ApplyOutcome.Failed($"Troca de resolução recusada pelo driver de vídeo ({result}).");
        }

        var confirmed = QueryCurrentMode();
        if (confirmed.dmPelsWidth != (uint)width || confirmed.dmPelsHeight != (uint)height)
        {
            return ApplyOutcome.Failed(
                $"O driver relatou sucesso, mas o modo lido de volta é "
                + $"{confirmed.dmPelsWidth}x{confirmed.dmPelsHeight}, não {width}x{height}.");
        }

        return ApplyOutcome.Changed();
    }
}
