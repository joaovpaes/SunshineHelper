using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;

namespace SunshineHelper.Infrastructure.Display;

/// <summary>
/// Turns HDR ("Advanced color" in Windows' own wording) on or off for the primary
/// display, captures what it was and puts it back. There is no public, documented API
/// for this — Windows' own Settings app and third-party tools such as
/// <c>HDRSwitch.exe</c> go through the same Connecting-and-Configuring-Displays (CCD)
/// calls used here (<c>DisplayConfigGetDeviceInfo</c>/<c>DisplayConfigSetDeviceInfo</c>
/// with <c>DISPLAYCONFIG_..._ADVANCED_COLOR_...</c>); see ADR 0002. Scoped to the primary
/// display for v1, matching <see cref="PrimaryDisplayModule"/> and the single-monitor
/// setup Phase 0 was tested on (docs/fase0-resultados.md); picking the display by the
/// <c>device_id</c> Sunshine reports is future work for a multi-monitor setup.
/// </summary>
/// <remarks>
/// <para>
/// Turning HDR <b>off</b> is always attempted, even on a display that does not report
/// support for it: that is a no-op there, and it is exactly what a revert after a
/// captured "off" state needs. Turning it <b>on</b> is refused up front when the display
/// does not report support, with a clear message, rather than letting the driver call
/// fail in a more confusing way.
/// </para>
/// <para>
/// Confirmation is always a fresh read of the state through this module's own
/// <c>DisplayConfigGetDeviceInfo</c> call, immediately after changing it — never from an
/// external process (same reasoning as <see cref="PrimaryDisplayModule"/>:
/// docs/fase0-resultados.md, "Teste rápido do 4K").
/// </para>
/// </remarks>
public sealed class HdrModule : IEnvironmentModule
{
    private const int CurrentPayloadVersion = 1;

    // Bits inside the "value" field of the anonymous bitfield union on
    // DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO (wingdi.h) — read as a plain uint instead of
    // guessing at the generated bitfield property names.
    private const uint AdvancedColorSupportedBit = 0x1;
    private const uint AdvancedColorEnabledBit = 0x2;

    public string Id => "hdr";

    public int Order => 2; // After the resolution module (PrimaryDisplayModule.Order).

    // Windows restores the user's own HDR setting by itself; this module's state does
    // not survive a reboot.
    public bool SurvivesReboot => false;

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var (_, enabled) = QueryAdvancedColorState();

        var payload = new JsonObject { ["enabled"] = enabled };
        return Task.FromResult(new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload });
    }

    public Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        if (desired.Hdr is not { } wantEnabled)
        {
            return Task.FromResult(ApplyOutcome.NothingToDo());
        }

        return Task.FromResult(SetAdvancedColorState(wantEnabled));
    }

    public Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.PayloadVersion != CurrentPayloadVersion)
        {
            return Task.FromResult(RevertOutcome.Failed(
                $"O snapshot do módulo de HDR foi gravado numa versão ({snapshot.PayloadVersion}) "
                + $"que esta versão do app não lê (esperado {CurrentPayloadVersion})."));
        }

        if (snapshot.Payload is not { } payload)
        {
            return Task.FromResult(RevertOutcome.Failed("O snapshot do módulo de HDR não tem dados."));
        }

        bool enabled = payload["enabled"]!.GetValue<bool>();

        var outcome = SetAdvancedColorState(enabled);
        return Task.FromResult(outcome.Status == ApplyStatus.Failed
            ? RevertOutcome.Failed(outcome.ErrorMessage ?? "Falha desconhecida ao restaurar o HDR.")
            : RevertOutcome.Success());
    }

    private static unsafe (LUID AdapterId, uint TargetId) GetPrimaryTarget()
    {
        uint pathCount;
        uint modeCount;
        var sizeError = PInvoke.GetDisplayConfigBufferSizes(
            QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
            out pathCount,
            out modeCount);

        if (sizeError != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new InvalidOperationException(
                $"Não foi possível descobrir o tamanho da configuração de vídeo atual "
                + $"(GetDisplayConfigBufferSizes: {sizeError}).");
        }

        if (pathCount == 0)
        {
            throw new InvalidOperationException("Nenhuma tela ativa foi encontrada (QueryDisplayConfig).");
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        var queryError = PInvoke.QueryDisplayConfig(
            QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
            ref pathCount,
            paths,
            ref modeCount,
            modes,
            null);

        if (queryError != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Não foi possível ler a configuração de vídeo ativa (QueryDisplayConfig: {queryError}).");
        }

        // Scoped to the primary display for v1 (see class remarks): the first active path.
        var target = paths[0].targetInfo;
        return (target.adapterId, target.id);
    }

    private static (bool Supported, bool Enabled) QueryAdvancedColorState()
    {
        var (adapterId, targetId) = GetPrimaryTarget();

        var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                adapterId = adapterId,
                id = targetId,
            },
        };

        int result = PInvoke.DisplayConfigGetDeviceInfo(ref colorInfo.header);
        if (result != 0)
        {
            throw new InvalidOperationException($"Não foi possível ler o estado do HDR (DisplayConfigGetDeviceInfo: {result}).");
        }

        uint flags = colorInfo.Anonymous.value;
        return (
            Supported: (flags & AdvancedColorSupportedBit) != 0,
            Enabled: (flags & AdvancedColorEnabledBit) != 0);
    }

    private static ApplyOutcome SetAdvancedColorState(bool enable)
    {
        var (supported, _) = QueryAdvancedColorState();
        if (enable && !supported)
        {
            return ApplyOutcome.Failed("Este monitor não anuncia suporte a HDR (Advanced Color) para o Windows.");
        }

        var (adapterId, targetId) = GetPrimaryTarget();

        var setState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = adapterId,
                id = targetId,
            },
        };
        setState.Anonymous.value = enable ? 1u : 0u;

        int result = PInvoke.DisplayConfigSetDeviceInfo(ref setState.header);
        if (result != 0)
        {
            return ApplyOutcome.Failed($"Troca de HDR recusada pelo driver de vídeo (DisplayConfigSetDeviceInfo: {result}).");
        }

        var (_, confirmedEnabled) = QueryAdvancedColorState();
        if (confirmedEnabled != enable)
        {
            return ApplyOutcome.Failed(
                $"O driver relatou sucesso, mas o HDR lido de volta está "
                + $"{(confirmedEnabled ? "ligado" : "desligado")}, não {(enable ? "ligado" : "desligado")}.");
        }

        return ApplyOutcome.Changed();
    }
}
