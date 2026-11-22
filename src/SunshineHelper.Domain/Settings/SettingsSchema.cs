namespace SunshineHelper.Domain.Settings;

/// <summary>The settings.json schema version this build understands. Bump this and add
/// a matching <see cref="ISettingsMigration"/> whenever the shape changes.</summary>
public static class SettingsSchema
{
    public const int CurrentVersion = 1;
}
