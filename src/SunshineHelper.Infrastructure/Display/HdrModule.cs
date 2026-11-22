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
/// <c>DisplayConfigGetDeviceInfo</c> call, never from an external process (same reasoning
/// as <see cref="PrimaryDisplayModule"/>: docs/fase0-resultados.md, "Teste rápido do
/// 4K"). Unlike a resolution change, though, the read is retried for a moment: switching
/// HDR makes the display re-train — the screen goes black and comes back — and the state
/// read the instant the call returns can still be the old one. Reporting a failure there
/// would make the orchestrator roll back a session that actually worked.
/// </para>
/// </remarks>
public sealed class HdrModule : IEnvironmentModule
{
    private const int CurrentPayloadVersion = 1;

    // Bits inside the "value" field of the anonymous bitfield union on
    // DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO (wingdi.h) — read as a plain uint instead of
    // depending on the names generated for the bitfield members.
    private const uint AdvancedColorSupportedBit = 0x1;
    private const uint AdvancedColorEnabledBit = 0x2;

    private const int ConfirmTimeoutSeconds = 5;

    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(ConfirmTimeoutSeconds);
    private static readonly TimeSpan ConfirmInterval = TimeSpan.FromMilliseconds(150);

    private readonly TimeProvider _timeProvider;

    public HdrModule()
        : this(TimeProvider.System)
    {
    }

    public HdrModule(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Id => "hdr";

    public int Order => 2; // After the resolution module (PrimaryDisplayModule.Order).

    // Windows restores the user's own HDR setting by itself; this module's state does
    // not survive a reboot.
    public bool SurvivesReboot => false;

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var (_, enabled) = QueryAdvancedColorState(GetPrimaryTarget());

        var payload = new JsonObject { ["enabled"] = enabled };
        return Task.FromResult(new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload });
    }

    public async Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        if (desired.Hdr is not { } wantEnabled)
        {
            return ApplyOutcome.NothingToDo();
        }

        return await SetAdvancedColorStateAsync(wantEnabled, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.PayloadVersion != CurrentPayloadVersion)
        {
            return RevertOutcome.Failed(
                $"O snapshot do módulo de HDR foi gravado numa versão ({snapshot.PayloadVersion}) "
                + $"que esta versão do app não lê (esperado {CurrentPayloadVersion}).");
        }

        if (snapshot.Payload is not { } payload)
        {
            return RevertOutcome.Failed("O snapshot do módulo de HDR não tem dados.");
        }

        bool enabled = payload["enabled"]!.GetValue<bool>();

        var outcome = await SetAdvancedColorStateAsync(enabled, cancellationToken).ConfigureAwait(false);
        return outcome.Status == ApplyStatus.Failed
            ? RevertOutcome.Failed(outcome.ErrorMessage ?? "Falha desconhecida ao restaurar o HDR.")
            : RevertOutcome.Success();
    }

    private static unsafe DisplayTarget GetPrimaryTarget()
    {
        var sizeError = PInvoke.GetDisplayConfigBufferSizes(
            QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
            out uint pathCount,
            out uint modeCount);

        if (sizeError != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new InvalidOperationException(
                $"Não foi possível descobrir o tamanho da configuração de vídeo atual "
                + $"(GetDisplayConfigBufferSizes: {sizeError}).");
        }

        if (pathCount == 0)
        {
            throw new InvalidOperationException("Nenhuma tela ativa foi encontrada (GetDisplayConfigBufferSizes).");
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        // QueryDisplayConfig's array parameters are raw pointers in the generated
        // bindings (no friendly Span<T> overload), so the arrays above are pinned and
        // addressed directly instead.
        WIN32_ERROR queryError;
        fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modes)
        {
            queryError = PInvoke.QueryDisplayConfig(
                QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount,
                pathsPtr,
                ref modeCount,
                modesPtr,
                null);
        }

        if (queryError != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Não foi possível ler a configuração de vídeo ativa (QueryDisplayConfig: {queryError}).");
        }

        // QueryDisplayConfig writes back how many entries it actually filled, which can
        // be fewer than the buffer asked for. Without this check, zero filled entries
        // would quietly hand the calls below an all-zero adapter and target.
        if (pathCount == 0)
        {
            throw new InvalidOperationException("Nenhuma tela ativa foi encontrada (QueryDisplayConfig).");
        }

        // Scoped to the primary display for v1 (see class remarks): the first active path.
        var target = paths[0].targetInfo;
        return new DisplayTarget(target.adapterId, target.id);
    }

    private static (bool Supported, bool Enabled) QueryAdvancedColorState(DisplayTarget target)
    {
        var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                adapterId = target.AdapterId,
                id = target.TargetId,
            },
        };

        // The header sits at offset 0 of the struct above, so passing it by reference
        // hands the native call the address of the whole packet — which is what it
        // expects, since it reads header.size bytes from there.
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

    private async Task<ApplyOutcome> SetAdvancedColorStateAsync(bool enable, CancellationToken cancellationToken)
    {
        var target = GetPrimaryTarget();

        var (supported, _) = QueryAdvancedColorState(target);
        if (enable && !supported)
        {
            return ApplyOutcome.Failed("Este monitor não anuncia suporte a HDR (Advanced Color) para o Windows.");
        }

        var setState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = target.AdapterId,
                id = target.TargetId,
            },
        };
        setState.Anonymous.value = enable ? 1u : 0u;

        // Same offset-0 reasoning as the read above; this one takes the packet as "in"
        // because the native side only reads it.
        int result = PInvoke.DisplayConfigSetDeviceInfo(in setState.header);
        if (result != 0)
        {
            return ApplyOutcome.Failed($"Troca de HDR recusada pelo driver de vídeo (DisplayConfigSetDeviceInfo: {result}).");
        }

        if (!await ConfirmAsync(target, enable, cancellationToken).ConfigureAwait(false))
        {
            return ApplyOutcome.Failed(
                $"O driver relatou sucesso, mas depois de {ConfirmTimeoutSeconds} s o HDR lido de volta "
                + $"continua {(enable ? "desligado" : "ligado")}.");
        }

        return ApplyOutcome.Changed();
    }

    /// <summary>Reads the state back until it matches, or until the timeout — the display
    /// re-trains after an HDR switch, so the first read can still be the old value.</summary>
    private async Task<bool> ConfirmAsync(DisplayTarget target, bool expected, CancellationToken cancellationToken)
    {
        long start = _timeProvider.GetTimestamp();

        while (true)
        {
            if (QueryAdvancedColorState(target).Enabled == expected)
            {
                return true;
            }

            if (_timeProvider.GetElapsedTime(start) >= ConfirmTimeout)
            {
                return false;
            }

            await Task.Delay(ConfirmInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private readonly record struct DisplayTarget(LUID AdapterId, uint TargetId);
}
