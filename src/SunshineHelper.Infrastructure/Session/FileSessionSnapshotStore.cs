using System.Text;
using System.Threading;
using SunshineHelper.Application.Session;

namespace SunshineHelper.Infrastructure.Session;

/// <summary>
/// The session snapshot on disk, at
/// <c>%LOCALAPPDATA%\SunshineHelper\state\current-session.json</c> by default.
/// </summary>
/// <remarks>
/// Writes go to a temp file that is flushed all the way to the disk before replacing the
/// real one, so a power cut can never leave a half-written snapshot: the file on disk is
/// always either the previous snapshot or the complete new one. This is the one file in
/// the app where that matters — without it there is no way back to the user's own
/// resolution, HDR and FPS settings (docs/adr/0002-arquitetura-do-nucleo.md, "Snapshot
/// atômico e orquestrador").
/// </remarks>
public sealed class FileSessionSnapshotStore : ISessionSnapshotStore
{
    private const int BufferSize = 4096;

    private readonly string _path;

    public FileSessionSnapshotStore()
        : this(DefaultPath())
    {
    }

    public FileSessionSnapshotStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunshineHelper",
        "state",
        "current-session.json");

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAtomicAsync(string content, CancellationToken cancellationToken)
    {
        EnsureDirectory();

        string tempPath = $"{_path}.tmp";
        var bytes = Encoding.UTF8.GetBytes(content);

        var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Reaches the physical disk, not just the OS cache: a power cut right after
            // this call must still leave a readable snapshot behind.
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(_path))
        {
            File.Replace(tempPath, _path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _path);
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return Task.CompletedTask;
    }

    public bool Exists() => File.Exists(_path);

    private void EnsureDirectory()
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
