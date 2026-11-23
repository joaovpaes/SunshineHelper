using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Session;

namespace SunshineHelper.Infrastructure.Tests.Channel;

/// <summary>An in-memory stand-in good enough for these pipe-plumbing tests, which only
/// need to drive <see cref="RestoreStatus.NothingToDo"/> through a real connection —
/// matching a profile and applying it is <see cref="TriggerChannelHandler"/>'s own job
/// and already covered in SunshineHelper.Application.Tests.</summary>
internal sealed class FakeSessionSnapshotStore : ISessionSnapshotStore
{
    private string? _content;

    public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(_content);

    public Task WriteAtomicAsync(string content, CancellationToken cancellationToken)
    {
        _content = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        _content = null;
        return Task.CompletedTask;
    }

    public bool Exists() => _content is not null;
}
