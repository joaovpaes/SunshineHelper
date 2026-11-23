using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Infrastructure.Tests.Channel;

internal sealed class FakeBootIdentityProvider : IBootIdentityProvider
{
    public BootIdentity? GetCurrent() => BootIdentity.Unknown;
}
