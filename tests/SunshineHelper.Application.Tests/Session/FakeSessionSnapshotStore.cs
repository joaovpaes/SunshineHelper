using System.Threading;
using SunshineHelper.Application.Session;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>The snapshot store in memory, with every write kept so a test can look at
/// what was on disk at any point of a session, and with failures that can be switched
/// on at a chosen write.</summary>
internal sealed class FakeSessionSnapshotStore : ISessionSnapshotStore
{
    public FakeSessionSnapshotStore(List<string>? log = null)
    {
        Log = log;
    }

    public string? Content { get; set; }

    public int WriteCount { get; private set; }

    public int DeleteCount { get; private set; }

    public List<string> Writes { get; } = [];

    public List<string>? Log { get; }

    /// <summary>Writes after this many succeed; the next one throws. Negative never
    /// fails.</summary>
    public int FailWriteAfter { get; set; } = -1;

    public Exception? ThrowOnRead { get; set; }

    public Exception? ThrowOnDelete { get; set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnRead is not null)
        {
            throw ThrowOnRead;
        }

        return Task.FromResult(Content);
    }

    public Task WriteAtomicAsync(string content, CancellationToken cancellationToken)
    {
        WriteCount++;
        Log?.Add("store:write");

        if (FailWriteAfter >= 0 && WriteCount > FailWriteAfter)
        {
            throw new IOException("Não há espaço em disco.");
        }

        Content = content;
        Writes.Add(content);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        Log?.Add("store:delete");

        if (ThrowOnDelete is not null)
        {
            throw ThrowOnDelete;
        }

        DeleteCount++;
        Content = null;
        return Task.CompletedTask;
    }

    public bool Exists() => Content is not null;
}
