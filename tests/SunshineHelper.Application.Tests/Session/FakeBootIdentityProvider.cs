using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Tests.Session;

internal sealed class FakeBootIdentityProvider : IBootIdentityProvider
{
    public BootIdentity? Current { get; set; } = new() { Id = "boot-1", UptimeMs = 3_600_000 };

    public Exception? Throws { get; set; }

    public BootIdentity? GetCurrent() => Throws is not null ? throw Throws : Current;
}
