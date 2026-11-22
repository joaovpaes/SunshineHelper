using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Settings;

/// <summary>What <see cref="SettingsService.LoadAsync"/> found: the settings to use
/// (defaults when nothing was on disk, or when the file could not be parsed at all),
/// whether they are valid, whether loading migrated the file on disk, and whether the
/// file didn't exist yet.</summary>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsValidationResult Validation,
    bool Migrated,
    bool Created);
