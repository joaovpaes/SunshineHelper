using System.Threading;

namespace SunshineHelper.Application.Monitoring;

/// <summary>
/// A stream of lines appended to the Sunshine log, oldest first, read as they are
/// written. Infrastructure supplies the real file-tailing implementation
/// (<c>SunshineLogFileSource</c>); tests feed a canned sequence instead.
/// </summary>
public interface ISunshineLogSource
{
    /// <summary>Ends only when <paramref name="cancellationToken"/> is cancelled — the
    /// log keeps being watched for as long as the app runs.</summary>
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken);
}
