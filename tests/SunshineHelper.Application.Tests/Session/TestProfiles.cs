using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Tests.Session;

internal static class TestProfiles
{
    /// <summary>A profile that follows whatever the client asks for.</summary>
    public static ClientProfile FollowClient() => new()
    {
        Id = "tv-4k",
        Name = "TV 4K",
        Display = new DisplayProfileSettings
        {
            Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.FollowClient },
            Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.FollowClient },
            FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.FollowClient },
        },
    };

    public static ClientRequest Request() => new()
    {
        Name = "Sala",
        Width = 3840,
        Height = 2160,
        Fps = 60,
        Hdr = true,
    };
}
