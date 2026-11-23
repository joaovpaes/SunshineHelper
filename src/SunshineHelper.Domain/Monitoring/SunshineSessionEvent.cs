namespace SunshineHelper.Domain.Monitoring;

/// <summary>
/// What one line of the Sunshine log means for session lifecycle
/// (docs/adr/0004-leitor-do-log.md). Every other line is ignored.
/// </summary>
public enum SunshineSessionEvent
{
    /// <summary><c>CLIENT CONNECTED</c>. Clears any pending disconnect/cancel signal —
    /// the client came back before anything reacted to it leaving.</summary>
    ClientConnected,

    /// <summary><c>CLIENT DISCONNECTED</c> — an ordinary disconnect, with no guarantee
    /// the client will not resume. Sunshine runs no Undo Command for this.</summary>
    ClientDisconnected,

    /// <summary><c>DESTINATION :: /cancel</c> — the client, or Moonlight's own "disconnect
    /// and close" action, asked Sunshine to end the session for good. The Undo Command
    /// that follows is the primary way the environment gets reverted
    /// (docs/adr/0003-canal-e-auxiliar-de-gatilho.md); the log reader only needs to react
    /// to this as a fallback.</summary>
    CancelRequested,

    /// <summary><c>Session ended</c> — closes out whichever of
    /// <see cref="ClientDisconnected"/> or <see cref="CancelRequested"/> came last. On its
    /// own this line does not say which of the two happened.</summary>
    SessionEnded,

    /// <summary><c>DESTINATION :: /resume</c> — the client reconnected to a session that
    /// was never torn down. Sunshine runs no Prep Command for this (docs/fase0-analise-de-logs.md),
    /// so this is the only signal available for reapplying a profile that was reverted
    /// while the client was away.</summary>
    Resumed,
}
