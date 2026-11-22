using System.Text.Json.Nodes;

namespace SunshineHelper.Domain.Settings;

/// <summary>Upgrades a settings.json written before <c>schemaVersion</c> existed
/// (version 0: the field is absent) to version 1: adds the field and fills in the
/// timing defaults from docs/adr/0002-arquitetura-do-nucleo.md.</summary>
public sealed class AddSchemaVersionMigration : ISettingsMigration
{
    public int FromVersion => 0;

    public JsonObject Migrate(JsonObject document)
    {
        var result = (JsonObject)document.DeepClone();
        result["schemaVersion"] = 1;

        if (result["timing"] is not JsonObject)
        {
            var defaults = new TimingSettings();
            result["timing"] = new JsonObject
            {
                ["applyTargetMs"] = defaults.ApplyTargetMs,
                ["barrierDefaultMs"] = defaults.BarrierDefaultMs,
                ["barrierCeilingMs"] = defaults.BarrierCeilingMs,
                ["launcherDelayMs"] = defaults.LauncherDelayMs,
            };
        }

        result["profiles"] ??= new JsonArray();

        return result;
    }
}
