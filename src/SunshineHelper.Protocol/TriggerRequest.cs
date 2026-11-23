namespace SunshineHelper.Protocol;

/// <summary>
/// One line of JSON sent by the auxiliary (docs/adr/0002-arquitetura-do-nucleo.md, "Canal
/// entre o auxiliar e o app"). A single shape for both operations, with the fields each one
/// does not use left null, rather than two separate message types — it mirrors the ADR's
/// own examples and avoids polymorphic JSON, which plays badly with NativeAOT's trimming.
/// </summary>
public sealed record TriggerRequest
{
    public int V { get; init; } = ProtocolVersion.Current;

    /// <summary>One of <see cref="TriggerOp"/>.</summary>
    public required string Op { get; init; }

    /// <summary><see cref="TriggerOp.SessionStart"/> only.</summary>
    public ClientInfo? Client { get; init; }

    /// <summary>
    /// <see cref="TriggerOp.SessionStart"/> only: how long, in milliseconds, the auxiliary
    /// is willing to wait for a reply before it gives up and exits 0 anyway
    /// (docs/adr/0002-arquitetura-do-nucleo.md, "Orçamento de tempo"). The server races its
    /// own work against this same budget and replies <see cref="ChannelState.Timeout"/>
    /// instead of blocking the auxiliary past it — the work itself keeps running either way.
    /// </summary>
    public int? WaitMs { get; init; }

    /// <summary><see cref="TriggerOp.SessionEnd"/> only: one of <see cref="SessionEndReason"/>.
    /// Absent means <see cref="SessionEndReason.Cancel"/>, which is what the auxiliary's Undo
    /// Command always is; only <see cref="SessionEndReason.Disconnect"/> changes anything
    /// (the launcher is left open).</summary>
    public string? Reason { get; init; }
}
