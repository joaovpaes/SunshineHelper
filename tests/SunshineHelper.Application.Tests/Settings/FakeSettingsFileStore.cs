using System.Threading;
using SunshineHelper.Application.Settings;

namespace SunshineHelper.Application.Tests.Settings;

/// <summary>An in-memory <see cref="ISettingsFileStore"/>, so <see cref="SettingsService"/>
/// tests never touch the real file system.</summary>
public sealed class FakeSettingsFileStore : ISettingsFileStore
{
    private readonly Dictionary<string, string> _files = [];

    public int WriteCount { get; private set; }

    public int CopyCount { get; private set; }

    public void Seed(string path, string content) => _files[path] = content;

    public string? Get(string path) => _files.GetValueOrDefault(path);

    public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(_files.GetValueOrDefault(path));

    public Task WriteAllTextAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        _files[path] = content;
        WriteCount++;
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (_files.ContainsKey(destinationPath))
        {
            throw new IOException($"'{destinationPath}' já existe.");
        }

        _files[destinationPath] = _files[sourcePath];
        CopyCount++;
        return Task.CompletedTask;
    }

    public bool Exists(string path) => _files.ContainsKey(path);
}
