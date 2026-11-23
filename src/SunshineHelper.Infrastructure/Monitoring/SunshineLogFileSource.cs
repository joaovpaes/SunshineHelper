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

            using FileStream? stream = TryOpen();
            if (stream is null)
            {
                // Momentarily gone or locked — by a brief exclusive lock while something
                // else rotates it, or an antivirus scan, for instance. Position is left
                // untouched: resetting it here on every transient failure would replay
                // old lines as if they just happened. A real rotation (the file coming
                // back shorter) is detected below, once it can be opened again.
                await DelayAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (stream.Length < position)
            {
                // Sunshine replaced the file with a shorter one (restarted, log
                // rotated): the position from the old file means nothing here.
                position = 0;
            }

            int appended = checked((int)(stream.Length - position));
            if (appended > 0)
            {
                stream.Seek(position, SeekOrigin.Begin);
                byte[] buffer = new byte[appended];
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);

                int consumed = 0;
                int lineStart = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] != (byte)'\n')
                    {
                        continue;
                    }

                    int lineLength = i - lineStart;
                    if (lineLength > 0 && buffer[lineStart + lineLength - 1] == (byte)'\r')
                    {
                        lineLength--;
                    }

                    yield return Encoding.UTF8.GetString(buffer, lineStart, lineLength);
                    lineStart = i + 1;
                    consumed = lineStart;
                }

                // A trailing, not-yet-newline-terminated chunk is not a line yet — it is
                // whatever Sunshine was in the middle of writing. Left unconsumed, it is
                // read again, as a prefix of itself, once the rest of it lands.
                position += consumed;
            }

            await DelayAsync(cancellationToken).ConfigureAwait(false);
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

    private FileStream? TryOpen()
    {
        try
        {
            return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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

    private Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(_pollIntervalMs, cancellationToken);
}
