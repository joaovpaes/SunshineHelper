using System.Text.Json.Nodes;

namespace SunshineHelper.Domain.Settings;

/// <summary>One step in the settings.json migration chain: upgrades the raw JSON from
/// <see cref="FromVersion"/> to the next version, before it is deserialized into
/// <see cref="AppSettings"/>. Add one implementation per schema bump instead of
/// branching inside the loader.</summary>
public interface ISettingsMigration
{
    int FromVersion { get; }

    JsonObject Migrate(JsonObject document);
}
