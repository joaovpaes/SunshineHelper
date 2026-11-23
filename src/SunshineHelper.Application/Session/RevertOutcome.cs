namespace SunshineHelper.Application.Session;

/// <summary>What a module reports back from putting its own state back.</summary>
public sealed record RevertOutcome(bool Reverted, string? ErrorMessage = null)
{
    public static RevertOutcome Success() => new(true);

    public static RevertOutcome Failed(string error) => new(false, error);

    /// <summary>Back as far as it matters, with something the user should hear about
    /// (a launcher process that would not close, for instance). The orchestrator reports
    /// the message but still treats the module as reverted, so the snapshot is cleared.</summary>
    public static RevertOutcome SucceededWithWarning(string warning) => new(true, warning);
}
