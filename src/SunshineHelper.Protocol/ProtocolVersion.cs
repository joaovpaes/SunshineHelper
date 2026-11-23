namespace SunshineHelper.Protocol;

/// <summary>
/// The <c>"v"</c> field every message on the channel carries
/// (docs/adr/0002-arquitetura-do-nucleo.md, "Canal entre o auxiliar e o app"). Today there
/// is only one version, so a mismatch simply means "I don't speak this" rather than
/// anything more nuanced — the graceful-degradation story starts the day a second version
/// exists and some fields need to stay optional across both.
/// </summary>
public static class ProtocolVersion
{
    public const int Current = 1;
}
