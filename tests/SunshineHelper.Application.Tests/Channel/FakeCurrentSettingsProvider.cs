using SunshineHelper.Application.Channel;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Tests.Channel;

/// <summary>Just holds whatever <see cref="AppSettings"/> the test gave it — the real
/// loading/refreshing story belongs to a future UI-wiring PR, not to
/// <see cref="TriggerChannelHandler"/>'s own tests.</summary>
internal sealed class FakeCurrentSettingsProvider(AppSettings settings) : ICurrentSettingsProvider
{
    public AppSettings Current { get; set; } = settings;
}
