using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunshineHelper.Application.Settings;

/// <summary>The one <see cref="JsonSerializerOptions"/> settings.json is read and
/// written with: camelCase properties (matching docs/adr examples), enums as
/// camelCase strings, indented for a human editing the file by hand.</summary>
public static class SettingsJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
