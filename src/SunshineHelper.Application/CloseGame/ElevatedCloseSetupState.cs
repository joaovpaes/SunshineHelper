using SunshineHelper.Protocol;

namespace SunshineHelper.Application.CloseGame;

/// <summary>Where the permission to close games that run as administrator stands
/// (docs/adr/0017-fechar-jogo-de-administrador.md, section 5), for the General screen.</summary>
public enum ElevatedCloseSetupState
{
    /// <summary>Not allowed yet; the button can allow it.</summary>
    NotAllowed,

    /// <summary>Allowed and up to date.</summary>
    Allowed,

    /// <summary>Allowed, but the installed auxiliary differs from the one next to the app (the app
    /// was updated); allowing again updates it.</summary>
    Outdated,

    /// <summary>Not allowed, and this Windows account is not an administrator: "highest
    /// privileges" would not raise anything.</summary>
    NotAdminAccount,

    /// <summary>Not allowed, and the auxiliary is not next to the app, so there is nothing to
    /// install from.</summary>
    AuxiliaryMissing,
}

/// <summary>What the app found on the PC; <see cref="Decide"/> turns it into a state.</summary>
/// <param name="AdminAccount">The Windows account is an administrator (even if the app itself
/// runs without elevation).</param>
/// <param name="BundledPresent">The auxiliary is next to the app.</param>
/// <param name="InstalledPresent">The auxiliary is in the protected install folder.</param>
/// <param name="TaskRegistered">The scheduled task exists.</param>
/// <param name="SameFile">The installed auxiliary is byte for byte the one next to the app.</param>
public sealed record ElevatedCloseSetupFacts(
    bool AdminAccount,
    bool BundledPresent,
    bool InstalledPresent,
    bool TaskRegistered,
    bool SameFile)
{
    /// <summary>The state to show. Allowed means both the installed copy and the task exist; a
    /// half install (one without the other) counts as not allowed, and allowing again repairs it.</summary>
    public ElevatedCloseSetupState Decide()
    {
        if (InstalledPresent && TaskRegistered)
        {
            return !BundledPresent || SameFile ? ElevatedCloseSetupState.Allowed : ElevatedCloseSetupState.Outdated;
        }

        if (!AdminAccount)
        {
            return ElevatedCloseSetupState.NotAdminAccount;
        }

        return BundledPresent ? ElevatedCloseSetupState.NotAllowed : ElevatedCloseSetupState.AuxiliaryMissing;
    }
}

/// <summary>How a click on "Permitir" or "Remover" went.</summary>
public enum ElevatedCloseSetupOutcome
{
    /// <summary>Done.</summary>
    Done,

    /// <summary>The user said no to the UAC prompt.</summary>
    Canceled,

    /// <summary>It ran and failed, or could not start; see the exit code.</summary>
    Failed,
}

/// <summary>The outcome of allowing or removing, with the auxiliary's exit code when it ran.</summary>
public sealed record ElevatedCloseSetupResult(ElevatedCloseSetupOutcome Outcome, ElevatedCloseSetupExitCode? ExitCode = null);
