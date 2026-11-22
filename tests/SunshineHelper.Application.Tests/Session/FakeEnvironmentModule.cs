using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>
/// A module that owns one slice of a make-believe environment: a single string it
/// captures, overwrites on apply and puts back on revert. Every way a real module can
/// misbehave can be switched on here — throwing, reporting a failure, leaving the
/// environment half-changed — which is what the Phase 1 gate asks for: an injected
/// failure in each module, one at a time, with the environment back where it started.
/// </summary>
internal sealed class FakeEnvironmentModule : IEnvironmentModule
{
    public const string Original = "original";
    public const string Applied = "applied";
    public const string HalfApplied = "half-applied";

    public FakeEnvironmentModule(string id, int order, List<string>? log = null)
    {
        Id = id;
        Order = order;
        Log = log;
    }

    public string Id { get; }

    public int Order { get; }

    public bool SurvivesReboot { get; set; }

    /// <summary>The part of the environment this module is responsible for.</summary>
    public string Value { get; private set; } = Original;

    public int PayloadVersion { get; set; } = 1;

    public List<string>? Log { get; }

    public Exception? CaptureThrows { get; set; }

    public Exception? ApplyThrows { get; set; }

    public string? ApplyFails { get; set; }

    /// <summary>Changes the environment and only then reports the failure, like a real
    /// module that dies partway through.</summary>
    public bool ApplyFailsHalfway { get; set; }

    public bool ApplyDoesNothing { get; set; }

    public Exception? RevertThrows { get; set; }

    public string? RevertFails { get; set; }

    /// <summary>Runs inside ApplyAsync, before anything is changed — the moment a crash
    /// would happen.</summary>
    public Action? WhileApplying { get; set; }

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        Record("capture");

        if (CaptureThrows is not null)
        {
            throw CaptureThrows;
        }

        return Task.FromResult(new ModuleSnapshot
        {
            PayloadVersion = PayloadVersion,
            Payload = new JsonObject { ["value"] = Value },
        });
    }

    public Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        Record("apply");
        WhileApplying?.Invoke();

        if (ApplyThrows is not null)
        {
            throw ApplyThrows;
        }

        if (ApplyDoesNothing)
        {
            return Task.FromResult(ApplyOutcome.NothingToDo());
        }

        if (ApplyFails is { } applyFailure)
        {
            if (ApplyFailsHalfway)
            {
                Value = HalfApplied;
            }

            return Task.FromResult(ApplyOutcome.Failed(applyFailure));
        }

        Value = Applied;
        return Task.FromResult(ApplyOutcome.Changed());
    }

    /// <summary>Runs inside RevertAsync, before anything is put back.</summary>
    public Action? WhileReverting { get; set; }

    public Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken)
    {
        Record("revert");
        WhileReverting?.Invoke();

        if (RevertThrows is not null)
        {
            throw RevertThrows;
        }

        if (RevertFails is { } revertFailure)
        {
            return Task.FromResult(RevertOutcome.Failed(revertFailure));
        }

        Value = snapshot.Payload!["value"]!.GetValue<string>();
        return Task.FromResult(RevertOutcome.Success());
    }

    private void Record(string step) => Log?.Add($"{Id}:{step}");
}
