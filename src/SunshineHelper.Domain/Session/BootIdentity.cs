namespace SunshineHelper.Domain.Session;

/// <summary>
/// Which boot of the PC a snapshot belongs to. Two independent facts, because neither is
/// conclusive on its own: <see cref="Id"/> names the boot, and <see cref="UptimeMs"/> is
/// monotonic within a boot and resets when the PC restarts, so it still tells the truth
/// when the system clock is adjusted. <see cref="RecoveryDecider"/> combines them.
/// </summary>
public sealed record BootIdentity
{
    /// <summary>An identifier for the current boot, stable for as long as the PC stays
    /// up. How it is produced is the platform's business.</summary>
    public required string Id { get; init; }

    /// <summary>Milliseconds the PC has been up. Never decreases within a boot.</summary>
    public required long UptimeMs { get; init; }

    /// <summary>Stored when the platform cannot tell which boot this is. A snapshot
    /// carrying it is still safe to act on: <see cref="RecoveryDecider"/> falls back to
    /// comparing the uptime against the time elapsed since the snapshot was written.</summary>
    public static BootIdentity Unknown { get; } = new() { Id = "unknown", UptimeMs = 0 };
}
