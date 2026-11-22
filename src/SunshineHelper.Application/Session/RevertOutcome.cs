namespace SunshineHelper.Application.Session;

/// <summary>What a module reports back from putting its own state back.</summary>
public sealed record RevertOutcome(bool Reverted, string? ErrorMessage = null)
{
    public static RevertOutcome Success() => new(true);

    public static RevertOutcome Failed(string error) => new(false, error);
}
