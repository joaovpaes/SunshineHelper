namespace SunshineHelper.Domain.Settings;

/// <summary>A screen resolution, in pixels.</summary>
public sealed record Resolution(int Width, int Height);

/// <summary>
/// The video/HDR/FPS part of a profile. The refresh rate is deliberately absent:
/// the app never changes it (see the project's README).
/// </summary>
public sealed record DisplayProfileSettings
{
    public required ProfileValue<Resolution> Resolution { get; init; }

    public required ProfileValue<bool> Hdr { get; init; }

    public required ProfileValue<int> FpsLimit { get; init; }
}
