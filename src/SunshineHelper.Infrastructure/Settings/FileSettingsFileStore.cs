using System.Threading;
using SunshineHelper.Application.Settings;

namespace SunshineHelper.Infrastructure.Settings;

/// <summary>Real settings.json storage on disk. Writes are atomic (temp file +
/// <c>File.Replace</c>), matching the orchestrator's own snapshot write described in
/// docs/adr/0002-arquitetura-do-nucleo.md, so a crash mid-write never leaves a
/// half-written settings.json behind.</summary>
public sealed class FileSettingsFileStore : ISettingsFileStore
{
    public async Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllTextAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        EnsureDirectoryFor(path);

        var tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    public Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectoryFor(destinationPath);
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return Task.CompletedTask;
    }

    public bool Exists(string path) => File.Exists(path);

    private static void EnsureDirectoryFor(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
