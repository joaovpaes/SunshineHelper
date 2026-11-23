using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Launcher;

/// <summary>
/// The launcher safety net (docs/adr/0008-modulo-launcher.md, from ADR 0002 "Fechamento do
/// launcher"). Sunshine opens the launcher itself, after this app's Prep Command returns,
/// and at the end of a session it sometimes fails to close it cleanly. This module:
/// <list type="bullet">
/// <item>Capture: writes down which launcher processes were <em>already</em> running —
/// those belong to the user and are never closed.</item>
/// <item>Apply: waits the configured launcher delay before letting Sunshine go on.</item>
/// <item>Revert: closes launcher processes that started during the session — politely
/// first, then the process tree once the timeout runs out.</item>
/// </list>
/// </summary>
/// <remarks>
/// Applied last and therefore reverted first, so the launcher is gone before the screen
/// goes back to the user's own resolution. A launcher that refuses to die is reported as a
/// warning, not a failure: keeping the snapshot for its sake would block every later
/// session over a process the user can close by hand.
/// </remarks>
public sealed class LauncherModule : IEnvironmentModule
{
    public const string ModuleId = "launcher";

    private const int CurrentPayloadVersion = 1;

    /// <summary>Slack for clocks: a start time read from Windows and "now" read here can
    /// disagree by a few milliseconds.</summary>
    private static readonly TimeSpan _startTimeTolerance = TimeSpan.FromSeconds(1);

    private readonly IProcessController _processes;
    private readonly ICurrentSettingsProvider _settings;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public LauncherModule(IProcessController processes, ICurrentSettingsProvider settings)
        : this(processes, settings, TimeProvider.System, Task.Delay)
    {
    }

    public LauncherModule(
        IProcessController processes,
        ICurrentSettingsProvider settings,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _processes = processes;
        _settings = settings;
        _timeProvider = timeProvider;
        _delay = delay;
    }

    public string Id => ModuleId;

    public int Order => 4; // After display (1), HDR (2) and FPS (3): reverted first.

    /// <summary>Processes do not outlive a restart: after a reboot there is nothing to close.</summary>
    public bool SurvivesReboot => false;

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var launcher = _settings.Current.Launcher;
        var names = UsableNames(launcher.ProcessNames);
        IReadOnlyList<ProcessIdentity> alreadyRunning = names.Count == 0 ? [] : _processes.FindByNames(names);

        var payload = new JsonObject
        {
            ["processNames"] = new JsonArray([.. names.Select(name => (JsonNode?)name)]),
            ["closeTimeoutMs"] = launcher.CloseTimeoutMs,
            ["capturedAt"] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            ["alreadyRunning"] = new JsonArray([.. alreadyRunning.Select(process => (JsonNode?)new JsonObject
            {
                ["id"] = process.Id,
                ["startedAt"] = process.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            })]),
        };

        return Task.FromResult(new ModuleSnapshot { PayloadVersion = CurrentPayloadVersion, Payload = payload });
    }

    public async Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desired);

        var current = _settings.Current;
        int delayMs = desired.LauncherDelayMs ?? current.Timing.LauncherDelayMs;
        if (delayMs > 0)
        {
            // Holds the Prep Command, and with it Sunshine's launch of the app. The
            // auxiliary's own budget still caps the wait (ADR 0002, "Orçamento de tempo").
            await _delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
        }

        // "Changed" only so that Revert runs at the end of the session; nothing on the
        // PC has been touched by this module yet.
        return UsableNames(current.Launcher.ProcessNames).Count > 0
            ? ApplyOutcome.Changed()
            : ApplyOutcome.NothingToDo();
    }

    public async Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.PayloadVersion != CurrentPayloadVersion)
        {
            return RevertOutcome.Failed(
                $"O snapshot do módulo do launcher foi gravado numa versão ({snapshot.PayloadVersion}) "
                + $"que esta versão do app não lê (esperado {CurrentPayloadVersion}).");
        }

        if (!TryReadPayload(snapshot.Payload, out var captured))
        {
            return RevertOutcome.Failed("O snapshot do módulo do launcher está incompleto.");
        }

        // Names come from the snapshot, not from today's settings: the policy in force
        // when the session started is the one that decides what it may close.
        var names = UsableNames(captured.Names);
        if (names.Count == 0)
        {
            return RevertOutcome.Success();
        }

        var toClose = _processes.FindByNames(names)
            .Where(process => !captured.AlreadyRunning.Contains((process.Id, process.StartedAt)))
            .Where(process => process.StartedAt >= captured.CapturedAt - _startTimeTolerance)
            .ToList();

        var stubborn = new List<string>();
        var timeout = TimeSpan.FromMilliseconds(captured.CloseTimeoutMs);

        foreach (var process in toClose)
        {
            if (!await CloseAsync(process, timeout, cancellationToken).ConfigureAwait(false))
            {
                stubborn.Add(string.Create(CultureInfo.InvariantCulture, $"{process.Name} (PID {process.Id})"));
            }
        }

        return stubborn.Count == 0
            ? RevertOutcome.Success()
            : RevertOutcome.SucceededWithWarning(
                $"Não foi possível encerrar: {string.Join(", ", stubborn)}. Feche manualmente se continuar aberto.");
    }

    private static List<string> UsableNames(IEnumerable<string?> names) =>
        names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => LauncherSettings.NormalizeName(name!))
            .Where(name => name.Length > 0 && !LauncherSettings.ForbiddenNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool TryReadPayload(JsonObject? payload, out CapturedLauncher captured)
    {
        captured = default;
        if (payload?["processNames"] is not JsonArray names
            || payload["closeTimeoutMs"]?.GetValue<int>() is not { } timeoutMs
            || payload["capturedAt"]?.GetValue<string>() is not { } capturedAtText
            || payload["alreadyRunning"] is not JsonArray running
            || !DateTimeOffset.TryParse(capturedAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var capturedAt))
        {
            return false;
        }

        var alreadyRunning = new HashSet<(int, DateTimeOffset)>();
        foreach (var entry in running.OfType<JsonObject>())
        {
            if (entry["id"]?.GetValue<int>() is { } id
                && entry["startedAt"]?.GetValue<string>() is { } startedText
                && DateTimeOffset.TryParse(startedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var started))
            {
                alreadyRunning.Add((id, started));
            }
        }

        captured = new CapturedLauncher(
            [.. names.Select(node => node?.GetValue<string>())],
            Math.Max(timeoutMs, 1),
            capturedAt,
            alreadyRunning);
        return true;
    }

    /// <summary>Polite first, then the whole tree. False only if the process is still
    /// there after both.</summary>
    private async Task<bool> CloseAsync(ProcessIdentity process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            if (_processes.RequestClose(process)
                && await _processes.WaitForExitAsync(process, timeout, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            return await _processes.KillTreeAsync(process, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One stubborn or protected process must not stop the others from closing.
            return false;
        }
    }

    private readonly record struct CapturedLauncher(
        IReadOnlyList<string?> Names,
        int CloseTimeoutMs,
        DateTimeOffset CapturedAt,
        HashSet<(int Id, DateTimeOffset StartedAt)> AlreadyRunning);
}
