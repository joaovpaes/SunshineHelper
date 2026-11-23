namespace SunshineHelper.Domain.Settings;

/// <summary>One per-client profile: display settings plus an optional override of the
/// global launcher delay (<see cref="TimingSettings.LauncherDelayMs"/>).</summary>
public sealed record ClientProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ClientMatchRule Match { get; init; } = new();

    public required DisplayProfileSettings Display { get; init; }

    /// <summary>Overrides <see cref="TimingSettings.LauncherDelayMs"/> for this profile.
    /// Null means "use the global default".</summary>
    public int? LauncherDelayMs { get; init; }

    /// <summary>What to do when this client disconnects without a <c>/cancel</c>
    /// (docs/adr/0002-arquitetura-do-nucleo.md, "Desconexão e retomada"). Default
    /// <c>false</c> keeps the environment applied, in case the client resumes — exactly
    /// what Sunshine itself does, since it runs no Undo Command for a plain disconnect.
    /// <c>true</c> reverts right away and reapplies this profile if the client sends
    /// <c>/resume</c> later.</summary>
    public bool RevertOnDisconnect { get; init; }
}
