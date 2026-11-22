namespace SunshineHelper.Application.Session;

public enum ApplyStatus
{
    /// <summary>The module changed something and must be reverted at the end.</summary>
    Changed,

    /// <summary>The profile asked for no change in this module, so nothing was touched
    /// and nothing will be reverted.</summary>
    NothingToDo,

    Failed,
}

/// <summary>What a module reports back from applying a profile.</summary>
public sealed record ApplyOutcome(ApplyStatus Status, string? ErrorMessage = null)
{
    public static ApplyOutcome Changed() => new(ApplyStatus.Changed);

    public static ApplyOutcome NothingToDo() => new(ApplyStatus.NothingToDo);

    public static ApplyOutcome Failed(string error) => new(ApplyStatus.Failed, error);
}
