using System.Threading;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Session;

/// <summary>
/// One piece of the environment the app takes over during a session: the screen
/// resolution, HDR, the FPS cap, the launcher. Every module follows the same three
/// steps — read the current state, apply the profile, put the state back — which is what
/// keeps the orchestrator free of any knowledge about Windows
/// (docs/adr/0002-arquitetura-do-nucleo.md, "Módulos").
/// </summary>
/// <remarks>
/// A module may throw instead of reporting a failure; the orchestrator treats the two the
/// same way. What a module must never do is report success after a partial change: the
/// orchestrator reverts anything that failed, but it can only revert what it knows about.
/// </remarks>
public interface IEnvironmentModule
{
    /// <summary>Stable identifier, written into the snapshot and used to find this module
    /// again when restoring after a crash. Renaming it strands old snapshots.</summary>
    string Id { get; }

    /// <summary>Apply order. Reverting runs in the reverse order.</summary>
    int Order { get; }

    /// <summary>True when this module's state outlives a restart of the PC — the RTSS
    /// profile is a file on disk, while Windows restores resolution and HDR by itself.</summary>
    bool SurvivesReboot { get; }

    /// <summary>Reads the current state. Read-only: called before anything is changed,
    /// and a failure here stops the session before any module applies anything.</summary>
    Task<ModuleSnapshot> CaptureAsync(CancellationToken cancellationToken);

    /// <summary>Applies the part of <paramref name="desired"/> this module owns. A null
    /// value there means "leave it alone", which is reported as
    /// <see cref="ApplyStatus.NothingToDo"/>.</summary>
    Task<ApplyOutcome> ApplyAsync(DesiredEnvironment desired, CancellationToken cancellationToken);

    /// <summary>Puts back the state from <paramref name="snapshot"/>, which this module
    /// produced itself — possibly in an older version of the app, so the module checks
    /// <see cref="ModuleSnapshot.PayloadVersion"/> before trusting it.</summary>
    Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken cancellationToken);
}
