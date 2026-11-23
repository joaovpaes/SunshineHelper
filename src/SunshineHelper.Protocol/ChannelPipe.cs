namespace SunshineHelper.Protocol;

/// <summary>The named pipe both sides connect to (docs/adr/0002-arquitetura-do-nucleo.md,
/// "Canal entre o auxiliar e o app").</summary>
public static class ChannelPipe
{
    /// <summary>Passed as-is to <c>NamedPipeServerStream</c>/<c>NamedPipeClientStream</c>
    /// (no <c>\\.\pipe\</c> prefix — the BCL adds that).</summary>
    public const string Name = "SunshineHelper.v1";
}
