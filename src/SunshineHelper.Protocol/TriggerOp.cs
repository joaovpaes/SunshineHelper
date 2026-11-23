namespace SunshineHelper.Protocol;

/// <summary>The two operations the auxiliary sends (docs/adr/0002-arquitetura-do-nucleo.md,
/// "Canal entre o auxiliar e o app") — plain strings, not a JSON enum, so an unrecognized
/// future op still deserializes instead of throwing; <see cref="TriggerRequest.Op"/> is
/// checked against these constants by the handler.</summary>
public static class TriggerOp
{
    /// <summary>Sunshine's Prep Command (Do): a client connected.</summary>
    public const string SessionStart = "session-start";

    /// <summary>Sunshine's Prep Command (Undo): the stream ended. Sunshine only runs this
    /// on an explicit <c>/cancel</c>, not on a plain disconnect (docs/fase0-resultados.md)
    /// — an ordinary disconnect is detected a different way (the Sunshine log reader,
    /// Phase 1 PR 8), in-process, without going through this channel at all.</summary>
    public const string SessionEnd = "session-end";
}
