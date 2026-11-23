using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using SunshineHelper.Application.Monitoring;

namespace SunshineHelper.Infrastructure.Monitoring;

/// <summary>
/// Tails Sunshine's own log file by polling (docs/adr/0004-leitor-do-log.md). Polling,
/// not <see cref="FileSystemWatcher"/>, because it behaves the same way whether the file
/// does not exist yet, gets replaced on a Sunshine restart, or is held open for writing
/// by another process the whole time — a watcher needs separate handling for each of
/// those, and file-system watchers are also notoriously easy to lose events on.
/// </summary>
public sealed class SunshineLogFileSource : ISunshineLogSource
{
    /// <summary>Upper bound on one read. Sunshine appends a few lines per second at
    /// most; this only matters right after a rotation, when a large new file would
    /// otherwise be read in one allocation.</summary>
    private const int MaxChunkBytes = 1024 * 1024;

    private readonly string _path;
    private readonly int _pollIntervalMs;

    public SunshineLogFileSource(string path, int pollIntervalMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pollIntervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pollIntervalMs), pollIntervalMs, "Deve ser maior que zero.");
        }

        _path = path;
        _pollIntervalMs = pollIntervalMs;
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long position = await WaitForFileAsync(cancellationToken).ConfigureAwait(false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read first, with the file closed again before any line is handed out: the
            // consumer may spend seconds applying or reverting a profile per line, and
            // there is no reason to hold Sunshine's log open through that.
            AppendedChunk? chunk = await TryReadAppendedAsync(position, cancellationToken).ConfigureAwait(false);

            // Null: momentarily gone, locked or unreadable — a brief exclusive lock while
            // something rotates it, or an antivirus scan, for instance. Position is left
            // untouched: resetting it on every transient failure would replay old lines
            // as if they just happened. A real rotation (the file coming back shorter)
            // is detected once it can be read again.
            if (chunk is { } read)
            {
                position = read.Start;

                int consumed = 0;
                int lineStart = 0;
                for (int i = 0; i < read.Count; i++)
                {
                    if (read.Buffer[i] != (byte)'\n')
                    {
                        continue;
                    }

                    int lineLength = i - lineStart;
                    if (lineLength > 0 && read.Buffer[lineStart + lineLength - 1] == (byte)'\r')
                    {
                        lineLength--;
                    }

                    yield return Encoding.UTF8.GetString(read.Buffer, lineStart, lineLength);
                    lineStart = i + 1;
                    consumed = lineStart;
                }

                if (consumed == 0 && read.Count == MaxChunkBytes)
                {
                    // A whole chunk with no line break in it: not a log line this reader
                    // could ever match. Skipped, or it would be re-read forever.
                    consumed = read.Count;
                }

                // A trailing, not-yet-newline-terminated chunk is not a line yet — it is
                // whatever Sunshine was in the middle of writing. Left unconsumed, it is
                // read again, as a prefix of itself, once the rest of it lands.
                position += consumed;
            }

            await DelayAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads whatever was appended since <paramref name="position"/>, at most
    /// <see cref="MaxChunkBytes"/> at a time. Null when the file cannot be read right now,
    /// for any I/O reason — never throws for one, because a single bad poll must not end
    /// the watch for the rest of the app's lifetime.</summary>
    private async Task<AppendedChunk?> TryReadAppendedAsync(long position, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            await using (stream.ConfigureAwait(false))
            {
                // Read once: the file can keep changing while this runs.
                long length = stream.Length;
                if (length < position)
                {
                    // Sunshine replaced the file with a shorter one (restarted, log
                    // rotated): the position from the old file means nothing here.
                    position = 0;
                }

                int toRead = (int)Math.Min(length - position, MaxChunkBytes);
                if (toRead <= 0)
                {
                    return new AppendedChunk(position, [], 0);
                }

                stream.Seek(position, SeekOrigin.Begin);
                byte[] buffer = new byte[toRead];

                // Not ReadExactly: if the file shrinks between reading its length and
                // reading its bytes, fewer come back. Whatever did is still valid from
                // this position.
                int count = await stream
                    .ReadAtLeastAsync(buffer, toRead, throwOnEndOfStream: false, cancellationToken)
                    .ConfigureAwait(false);

                return new AppendedChunk(position, buffer, count);
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<long> WaitForFileAsync(CancellationToken cancellationToken)
    {
        while (!File.Exists(_path))
        {
            await DelayAsync(cancellationToken).ConfigureAwait(false);
        }

        // Start at the end: only new lines matter. Replaying the whole file on every app
        // start would replay stale CLIENT DISCONNECTED / Session ended / /resume lines
        // from the last time Windows was on, as if they had just happened.
        try
        {
            return new FileInfo(_path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(_pollIntervalMs, cancellationToken);

    /// <summary>The first <c>Count</c> bytes of <c>Buffer</c>, read from <c>Start</c> in
    /// the file — <c>Start</c> is zero when the file was found to have shrunk.</summary>
    private readonly record struct AppendedChunk(long Start, byte[] Buffer, int Count);
}
