using System.Threading;

namespace SunshineHelper.Application.Settings;

/// <summary>The minimal file-system access settings loading/saving needs, kept as an
/// interface so unit tests can fake it without touching disk. The real implementation
/// (plain file I/O, nothing Windows-specific) lives in
/// <c>SunshineHelper.Infrastructure.Settings.FileSettingsFileStore</c>.</summary>
public interface ISettingsFileStore
{
    /// <summary>Null when the file does not exist.</summary>
    Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken);

    /// <summary>Writes the whole file atomically (temp file + replace), creating the
    /// parent directory if needed.</summary>
    Task WriteAllTextAtomicAsync(string path, string content, CancellationToken cancellationToken);

    /// <summary>Copies a file, creating the destination directory if needed. Never
    /// overwrites an existing destination.</summary>
    Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken);

    bool Exists(string path);
}
