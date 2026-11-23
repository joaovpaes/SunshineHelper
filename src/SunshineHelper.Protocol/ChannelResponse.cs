namespace SunshineHelper.Protocol;

/// <summary>One line of JSON the app sends back (docs/adr/0002-arquitetura-do-nucleo.md,
/// "Canal entre o auxiliar e o app").</summary>
public sealed record ChannelResponse
{
    public int V { get; init; } = ProtocolVersion.Current;

    /// <summary>Whether the <i>exchange</i> succeeded — note that
    /// <see cref="ChannelState.Timeout"/> is still <c>true</c> here: the auxiliary's own
    /// rule is to exit 0 even then (docs/adr/0002-arquitetura-do-nucleo.md, "Regra
    /// absoluta"). Only <see cref="ChannelState.Failed"/> and a version mismatch set this
    /// to <c>false</c>.</summary>
    public required bool Ok { get; init; }

    /// <summary>One of <see cref="ChannelState"/>, or null when <see cref="Ok"/> is false
    /// for a reason that never got as far as picking a state (a version mismatch, a
    /// malformed request).</summary>
    public string? State { get; init; }

    /// <summary><see cref="TriggerOp.SessionStart"/>, state <see cref="ChannelState.Applied"/>
    /// only: how long applying actually took.</summary>
    public int? ElapsedMs { get; init; }

    /// <summary><see cref="TriggerOp.SessionStart"/>, state <see cref="ChannelState.Applied"/>
    /// only: the matched profile's display name, for logs on the auxiliary's side.</summary>
    public string? Profile { get; init; }

    /// <summary>Set whenever something did not go as asked; human-readable, in Portuguese,
    /// matching every other user-facing message in the app.</summary>
    public string? Error { get; init; }
}
