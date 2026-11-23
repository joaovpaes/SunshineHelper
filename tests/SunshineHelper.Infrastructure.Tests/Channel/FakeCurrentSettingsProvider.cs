using SunshineHelper.Application.Channel;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Infrastructure.Tests.Channel;

internal sealed class FakeCurrentSettingsProvider(AppSettings settings) : ICurrentSettingsProvider
{
    public AppSettings Current { get; } = settings;
}
