using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Domain.Session;

/// <summary>
/// Turns a profile plus what the client asked for into the concrete values to apply.
/// Pure, so the three modes a profile setting can be in
/// (<see cref="ProfileValueMode"/>) are testable on their own.
/// </summary>
public static class DesiredEnvironmentResolver
{
    public static DesiredEnvironment Resolve(ClientProfile profile, ClientRequest request)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);

        return new DesiredEnvironment
        {
            Resolution = ResolveResolution(profile.Display.Resolution, request),
            Hdr = ResolveHdr(profile.Display.Hdr, request),
            FpsLimit = ResolveFpsLimit(profile.Display.FpsLimit, request),
        };
    }

    private static Resolution? ResolveResolution(ProfileValue<Resolution> value, ClientRequest request) => value.Mode switch
    {
        // A fixed resolution with nothing set is rejected by SettingsValidator; if one
        // reaches here anyway, leaving the screen alone beats guessing at a size.
        ProfileValueMode.Fixed => value.FixedValue,
        ProfileValueMode.FollowClient when request.Width is { } width && request.Height is { } height
            => new Resolution(width, height),
        _ => null,
    };

    private static bool? ResolveHdr(ProfileValue<bool> value, ClientRequest request) => value.Mode switch
    {
        // FixedValue is a plain bool here, so an unset one is false: "force HDR off",
        // which is exactly what a fixed value of false should mean.
        ProfileValueMode.Fixed => value.FixedValue,
        ProfileValueMode.FollowClient => request.Hdr,
        _ => null,
    };

    private static int? ResolveFpsLimit(ProfileValue<int> value, ClientRequest request) => value.Mode switch
    {
        ProfileValueMode.Fixed when value.FixedValue > 0 => value.FixedValue,
        ProfileValueMode.FollowClient when request.Fps is > 0 => request.Fps,
        _ => null,
    };
}
