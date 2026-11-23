namespace SunshineHelper.Protocol;

/// <summary>Values for <see cref="TriggerRequest.Reason"/>. Absent or unknown is treated as
/// <see cref="Cancel"/>: the end of the session for real.</summary>
public static class SessionEndReason
{
    /// <summary>Sunshine's <c>/cancel</c>: the stream is over and the launcher may be
    /// closed.</summary>
    public const string Cancel = "cancel";

    /// <summary>A plain disconnect with the profile's "revert on disconnect" on. The
    /// client may still <c>/resume</c>, so the launcher stays open
    /// (docs/adr/0008-modulo-launcher.md). Only sent in-process, by the log reader.</summary>
    public const string Disconnect = "disconnect";

    /// <summary>The Sunshine process that was running when the session started is gone,
    /// so its Undo Command will never run: everything goes back, like <see cref="Cancel"/>
    /// (docs/adr/0013-sunshine-encerrado-restaura.md). Only sent in-process, by the
    /// watchdog.</summary>
    public const string SunshineExited = "sunshine-exited";
}
