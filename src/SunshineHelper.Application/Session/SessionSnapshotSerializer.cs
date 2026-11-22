using System.Text.Json;
using System.Text.Json.Serialization;
using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Session;

/// <summary>Converts the session snapshot to and from the JSON written to disk. Same
/// conventions as settings.json (camelCase, enums as strings, indented so a crashed
/// session can be read by a human), kept separate from
/// <see cref="Settings.SettingsJsonOptions"/> because the two files are versioned
/// independently.</summary>
public static class SessionSnapshotSerializer
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    public static string Serialize(SessionSnapshot snapshot) => JsonSerializer.Serialize(snapshot, _options);

    /// <summary>Reads a snapshot written by this or an older version of the app.</summary>
    /// <exception cref="SnapshotFormatException">The text is not valid JSON, does not
    /// match the expected shape, or carries a schema version this build does not
    /// know.</exception>
    public static SessionSnapshot Deserialize(string json)
    {
        SessionSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new SnapshotFormatException(
                $"O snapshot da sessão não corresponde ao formato esperado: {ex.Message}", ex);
        }

        if (snapshot is null)
        {
            throw new SnapshotFormatException("O snapshot da sessão está vazio.");
        }

        if (snapshot.SchemaVersion > SnapshotSchema.CurrentVersion)
        {
            throw new SnapshotFormatException(
                $"O snapshot da sessão foi gravado na versão {snapshot.SchemaVersion}, "
                + $"mais nova que a suportada ({SnapshotSchema.CurrentVersion}).");
        }

        if (snapshot.SchemaVersion < SnapshotSchema.CurrentVersion)
        {
            // No older envelope exists yet. When one does, migrate it here instead of
            // refusing — the snapshot is the only way back to the user's own settings.
            throw new SnapshotFormatException(
                $"O snapshot da sessão está na versão {snapshot.SchemaVersion}, "
                + $"sem caminho de leitura para a versão {SnapshotSchema.CurrentVersion}.");
        }

        return snapshot;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
