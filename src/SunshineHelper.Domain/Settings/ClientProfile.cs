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
}
