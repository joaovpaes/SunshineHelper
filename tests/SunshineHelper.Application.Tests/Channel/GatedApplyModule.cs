using System.Text.Json.Nodes;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Tests.Channel;

/// <summary>
/// A module whose apply hangs until the test lets it go. Needed because
/// <see cref="Session.FakeEnvironmentModule"/> finishes synchronously: with every fake
/// completing inline, <c>SessionOrchestrator.ApplyAsync</c> runs to the end before it
/// ever returns a task, so there is nothing for <c>TriggerChannelHandler</c>'s
/// <c>waitMs</c> to race against — blocking the thread with a sleep would not change
/// that, only a real yield does.
/// </summary>
internal sealed class GatedApplyModule : IEnvironmentModule
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Id => "gated";

    public int Order => 1;

    public bool SurvivesReboot => false;

    /// <summary>Lets the apply that is waiting inside <see cref="ApplyAsync"/> finish.</summary>
    public void Release() => _release.TrySetResult();

    public Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleSnapshot
        {
            PayloadVersion = 1,
            Payload = new JsonObject { ["value"] = "original" },
        });

    public async Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken)
    {
        await _release.Task.ConfigureAwait(false);
        return ApplyOutcome.Changed();
    }

    public Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken) =>
        Task.FromResult(RevertOutcome.Success());
}
