using System.Text.Json;
using System.Text.Json.Nodes;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Settings;

/// <summary>Converts between settings.json text, its raw <see cref="JsonObject"/> form
/// (used for migration, before the shape is assumed) and the typed
/// <see cref="AppSettings"/>.</summary>
public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions _options = SettingsJsonOptions.Create();

    public static JsonObject ParseRaw(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new SettingsFormatException("settings.json não é um JSON válido.", ex);
        }

        return node switch
        {
            null => throw new SettingsFormatException("settings.json está vazio."),
            JsonObject document => document,
            _ => throw new SettingsFormatException("settings.json não é um objeto JSON."),
        };
    }

    public static AppSettings Deserialize(JsonObject document)
    {
        try
        {
            return document.Deserialize<AppSettings>(_options)
                ?? throw new SettingsFormatException("settings.json não pôde ser interpretado.");
        }
        catch (JsonException ex)
        {
            throw new SettingsFormatException($"settings.json não corresponde ao formato esperado: {ex.Message}", ex);
        }
    }

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, _options);
}
