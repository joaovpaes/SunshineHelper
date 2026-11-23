using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Diagnostics;
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

    // Long enough for anything that re-applies the display's saved mode right after the
    // change (an HDR re-train, the driver) to have done so; the answer goes to the
    // diagnostic log only (docs/adr/0010-hdr-antes-da-resolucao.md).
    private const int LateReadbackDelayMs = 3_000;

    private readonly IDiagnosticLog _log;

    public PrimaryDisplayModule(IDiagnosticLog? log = null)
    {
        _log = log ?? NullDiagnosticLog.Instance;
    }

    public string Id => "display";

    // After HdrModule (1): an HDR switch resets the display mode, so the resolution has
    // to be set once HDR is settled (docs/adr/0010-hdr-antes-da-resolucao.md).
    public int Order => 2;

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
        var outcome = ChangeMode(target.Width, target.Height, (int)current.dmDisplayFrequency);
        Log(
            outcome.Status == ApplyStatus.Failed ? DiagnosticLevel.Error : DiagnosticLevel.Info,
            string.Create(
                CultureInfo.InvariantCulture,
                $"requested {target.Width}x{target.Height} (was {current.dmPelsWidth}x{current.dmPelsHeight} at {current.dmDisplayFrequency} Hz): {outcome.Status}{(outcome.ErrorMessage is { } failure ? " - " + failure : string.Empty)}"));

        if (outcome.Status == ApplyStatus.Changed)
        {
            _ = ReadBackLaterAsync(target.Width, target.Height);
        }

        return Task.FromResult(outcome);
    }

    private async Task ReadBackLaterAsync(int expectedWidth, int expectedHeight)
    {
        try
        {
            await Task.Delay(LateReadbackDelayMs).ConfigureAwait(false);
            var later = QueryCurrentMode();
            bool stillThere = later.dmPelsWidth == (uint)expectedWidth && later.dmPelsHeight == (uint)expectedHeight;
            Log(
                stillThere ? DiagnosticLevel.Info : DiagnosticLevel.Warning,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mode {LateReadbackDelayMs} ms after the change: {later.dmPelsWidth}x{later.dmPelsHeight} (expected {expectedWidth}x{expectedHeight}) - {(stillThere ? "unchanged since" : "SOMETHING CHANGED IT")}"));
        }
        catch (Exception ex)
        {
            Log(DiagnosticLevel.Warning, "late readback failed: " + ex.Message);
        }
    }

    private void Log(DiagnosticLevel level, string message) => _log.Write(level, "display", message);

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

    private static unsafe ApplyOutcome ChangeMode(int width, int height, int refreshRateHz)
    {
        var fields = DEVMODE_FIELD_FLAGS.DM_PELSWIDTH | DEVMODE_FIELD_FLAGS.DM_PELSHEIGHT;

        // Both 0 and 1 mean "whatever the driver's default is" rather than a real rate.
        // Asking for one of those literally can only confuse the driver; leaving the
        // field out entirely lets it keep doing what it was already doing, which is the
        // same outcome and is exactly what this app promises about the refresh rate.
        if (refreshRateHz > 1)
        {
            fields |= DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY;
        }

        var mode = new DEVMODEW
        {
            dmSize = (ushort)Marshal.SizeOf<DEVMODEW>(),
            dmFields = fields,
            dmPelsWidth = (uint)width,
            dmPelsHeight = (uint)height,
            dmDisplayFrequency = (uint)refreshRateHz,
        };

        // The device name and lParam are both null: the primary display, and no extra
        // parameters. lpDevMode is a raw pointer because Win32 allows null there to mean
        // "use the registry settings" — something a managed `ref` cannot express, which
        // is why this method is unsafe.
        var result = PInvoke.ChangeDisplaySettingsEx(
            default,
            &mode,
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
