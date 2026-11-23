using System.Threading;
using SunshineHelper.Application.Monitoring;

namespace SunshineHelper.Application.Tests.Monitoring;

/// <summary>Yields a fixed, canned sequence of lines and then ends — enough to prove
/// <see cref="SunshineSessionMonitor.RunAsync"/> parses and dispatches correctly; the
/// state-machine behavior itself is tested directly against <c>HandleAsync</c>.</summary>
internal sealed class FakeSunshineLogSource(IReadOnlyList<string> lines) : ISunshineLogSource
{
    public async IAsyncEnumerable<string> ReadLinesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
            await Task.Yield();
        }
    }
}
