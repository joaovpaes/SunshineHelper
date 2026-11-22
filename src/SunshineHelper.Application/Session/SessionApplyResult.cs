namespace SunshineHelper.Application.Session;

public enum SessionApplyStatus
{
    Applied,

    /// <summary>A snapshot was already on disk, so the environment the app would have
    /// captured is one it had already changed itself.</summary>
    Refused,

    Failed,
}

/// <summary>The outcome of applying a profile for one session.</summary>
public sealed record SessionApplyResult
{
    public required SessionApplyStatus Status { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Only meaningful when <see cref="Status"/> is
    /// <see cref="SessionApplyStatus.Failed"/>: true when the environment is back in the
    /// state the user had. False means a module could not be put back and the snapshot
    /// was kept on disk for a later attempt.</summary>
    public bool EnvironmentRestored { get; init; }

    /// <summary>How long the whole thing took. Compared against
    /// <c>timing.applyTargetMs</c> by the caller, which is the one that has the
    /// settings.</summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>Anything that went wrong along the way but did not by itself stop the
    /// session, ready to show to the user.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];
}
