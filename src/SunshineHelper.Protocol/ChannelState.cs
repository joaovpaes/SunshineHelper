namespace SunshineHelper.Protocol;

/// <summary>The <c>"state"</c> values a <see cref="ChannelResponse"/> carries.</summary>
public static class ChannelState
{
    /// <summary>Session start: the profile was applied before <c>waitMs</c> ran out.</summary>
    public const string Applied = "applied";

    /// <summary>Session end: the environment was put back before <c>waitMs</c> ran out.</summary>
    public const string Reverted = "reverted";

    /// <summary>Session end: there was nothing to put back (no session was running).</summary>
    public const string NothingToDo = "nothing-to-do";

    /// <summary>Either op: the real work is still running past <c>waitMs</c> — the
    /// auxiliary exits 0 anyway (docs/adr/0002-arquitetura-do-nucleo.md, "Orçamento de
    /// tempo"); the app keeps working in the background regardless of this reply.</summary>
    public const string Timeout = "timeout";

    /// <summary>Either op failed outright; <see cref="ChannelResponse.Error"/> says why.</summary>
    public const string Failed = "failed";
}
