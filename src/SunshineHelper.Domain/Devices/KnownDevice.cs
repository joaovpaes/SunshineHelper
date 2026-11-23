namespace SunshineHelper.Domain.Devices;

/// <summary>A device that has connected at least once, as Moonlight names it. Only these
/// few facts are kept: no IP address, no MAC, no Sunshine key (docs/adr/0014).</summary>
public sealed record KnownDevice
{
    public required string Name { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }

    public int ConnectionCount { get; init; }

    /// <summary>Name of the profile applied on the last connection; null when no profile
    /// matched, which is exactly the case where the user may want to create one.</summary>
    public string? LastProfileName { get; init; }
}
