namespace SunshineHelper.Domain.Settings;

/// <summary>The outcome of <see cref="SettingsValidator.Validate"/>: either no errors,
/// or every problem found (validation never stops at the first one, so the caller can
/// show everything wrong with settings.json at once).</summary>
public sealed record SettingsValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static readonly SettingsValidationResult Valid = new([]);
}
