namespace SunshineHelper.Domain.Session;

/// <summary>
/// What the connecting client asked for, as Sunshine hands it to the Prep Command in the
/// <c>SUNSHINE_CLIENT_*</c> variables (docs/fase0-resultados.md). Every field is optional:
/// older Sunshine versions, or a client that does not send something, simply leave it
/// unset, and a profile set to follow the client then leaves that setting alone.
/// </summary>
public sealed record ClientRequest
{
    /// <summary><c>SUNSHINE_CLIENT_NAME</c>. Any device Moonlight supports can appear
    /// here; this is the main signal for choosing a profile.</summary>
    public string? Name { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public int? Fps { get; init; }

    /// <summary><c>SUNSHINE_CLIENT_HDR</c>.</summary>
    public bool? Hdr { get; init; }
}
