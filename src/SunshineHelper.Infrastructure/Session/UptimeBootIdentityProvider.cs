using System.Globalization;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Infrastructure.Session;

/// <summary>
/// Boot identity derived from how long the PC has been up: the identity is the instant
/// the PC booted (now minus uptime, rounded to the minute to absorb jitter), and the
/// uptime itself travels alongside it.
/// </summary>
/// <remarks>
/// Rounding can make two readings of the same boot disagree when the computed instant
/// falls next to a minute boundary, and adjusting the system clock shifts it outright.
/// Neither is a problem: <see cref="RecoveryDecider"/> treats a differing identity as
/// proof of a reboot only when the uptime agrees, and otherwise assumes the same boot
/// and restores everything.
/// </remarks>
public sealed class UptimeBootIdentityProvider : IBootIdentityProvider
{
    private readonly TimeProvider _timeProvider;

    public UptimeBootIdentityProvider()
        : this(TimeProvider.System)
    {
    }

    public UptimeBootIdentityProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public BootIdentity? GetCurrent()
    {
        long uptimeMs = Environment.TickCount64;
        if (uptimeMs <= 0)
        {
            return null;
        }

        var bootInstant = _timeProvider.GetUtcNow().AddMilliseconds(-uptimeMs);
        var rounded = bootInstant.AddTicks(-(bootInstant.UtcTicks % TimeSpan.TicksPerMinute));

        string bootInstantText = rounded.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        return new BootIdentity
        {
            Id = $"uptime:{bootInstantText}",
            UptimeMs = uptimeMs,
        };
    }
}
