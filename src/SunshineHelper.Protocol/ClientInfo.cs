namespace SunshineHelper.Protocol;

/// <summary>What the connecting client asked for, straight from the
/// <c>SUNSHINE_CLIENT_*</c> variables the auxiliary reads (docs/fase0-resultados.md).
/// Every field is optional — an older Sunshine, or a client that omits something, still
/// produces a valid message.</summary>
public sealed record ClientInfo
{
    public string? Name { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public int? Fps { get; init; }

    public bool? Hdr { get; init; }
}
