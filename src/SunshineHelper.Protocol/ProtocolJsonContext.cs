using System.Text.Json.Serialization;

namespace SunshineHelper.Protocol;

/// <summary>
/// Source-generated serialization for every type on the channel. Required for
/// <c>SunshineHelper.Trigger</c>'s NativeAOT publish (reflection-based
/// <c>JsonSerializer</c> calls are trimmed away and throw at run time); the pipe server in
/// SunshineHelper.Infrastructure uses the same context so both sides serialize identically
/// without a second definition to keep in sync.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TriggerRequest))]
[JsonSerializable(typeof(ChannelResponse))]
[JsonSerializable(typeof(ClientInfo))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext
{
}
