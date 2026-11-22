using System.Text.Json.Nodes;

namespace SunshineHelper.Domain.Session;

/// <summary>
/// One module's own captured state, kept as opaque JSON so the core never needs to know
/// what any module stores: a new module ships without a single change here. The module
/// writes it in <c>CaptureAsync</c> and reads it back in <c>RevertAsync</c>.
/// </summary>
/// <remarks>
/// Two snapshots with identical JSON are not equal: <see cref="JsonObject"/> compares by
/// reference, so the compiler-generated record equality does too. Compare the fields you
/// care about instead of whole instances.
/// </remarks>
public sealed record ModuleSnapshot
{
    /// <summary>Version of this module's own payload format. The module checks it in
    /// <c>RevertAsync</c> and refuses, with a clear message, a payload written by a
    /// version it cannot read — rather than guessing and restoring the wrong state
    /// (docs/fase1-plano.md, "Riscos desta fase": RTSS changing its profile format).</summary>
    public required int PayloadVersion { get; init; }

    /// <summary>Null when the module had nothing to capture.</summary>
    public JsonObject? Payload { get; init; }
}
