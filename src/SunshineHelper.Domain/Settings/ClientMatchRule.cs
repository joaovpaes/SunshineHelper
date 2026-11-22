namespace SunshineHelper.Domain.Settings;

/// <summary>
/// How a profile is matched to a connecting client. Signal order follows
/// docs/adr/0002-arquitetura-do-nucleo.md, "Escolha do perfil": client name first,
/// IP range as a fallback. Any device Moonlight supports can be listed here — the
/// app is not limited to a fixed set of device types.
/// </summary>
public sealed record ClientMatchRule
{
    /// <summary>Client names Sunshine's <c>SUNSHINE_CLIENT_NAME</c> reports, matched
    /// case-insensitively (docs/fase0-resultados.md).</summary>
    public IReadOnlyList<string> ClientNames { get; init; } = [];

    /// <summary>CIDR ranges (e.g. <c>"192.168.1.0/24"</c>) used as a fallback when the
    /// client name is unavailable or does not match any profile.</summary>
    public IReadOnlyList<string> IpRanges { get; init; } = [];

    public bool IsEmpty => ClientNames.Count == 0 && IpRanges.Count == 0;
}
