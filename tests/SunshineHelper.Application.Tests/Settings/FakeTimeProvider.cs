namespace SunshineHelper.Application.Tests.Settings;

/// <summary>A controllable clock for the daily-backup tests.</summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
