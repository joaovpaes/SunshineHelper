using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Domain.Session;

/// <summary>
/// The concrete values a session should put in place, once the profile has been read
/// against what the client asked for. <b>Null always means "leave this alone"</b> — the
/// module skips it and nothing about it is reverted later.
/// </summary>
/// <remarks>
/// The refresh rate is deliberately absent: the app never changes it.
/// </remarks>
public sealed record DesiredEnvironment
{
    public Resolution? Resolution { get; init; }

    public bool? Hdr { get; init; }

    public int? FpsLimit { get; init; }

    /// <summary>This profile's own launcher delay, overriding
    /// <see cref="TimingSettings.LauncherDelayMs"/>; null uses the global value. A wait,
    /// not a change to the environment, so it does not count towards
    /// <see cref="IsEmpty"/>.</summary>
    public int? LauncherDelayMs { get; init; }

    /// <summary>Nothing in this session changes the environment.</summary>
    public bool IsEmpty => Resolution is null && Hdr is null && FpsLimit is null;
}
