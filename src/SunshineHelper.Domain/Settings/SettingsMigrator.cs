using System.Text.Json.Nodes;

namespace SunshineHelper.Domain.Settings;

/// <summary>The outcome of running the migration chain: the document at
/// <see cref="SettingsSchema.CurrentVersion"/>, and whether any step actually ran.</summary>
public sealed record MigrationResult(JsonObject Document, bool Migrated);

/// <summary>Runs the chain of <see cref="ISettingsMigration"/> steps, in order, until
/// the document reaches <see cref="SettingsSchema.CurrentVersion"/>.</summary>
public sealed class SettingsMigrator
{
    private readonly IReadOnlyDictionary<int, ISettingsMigration> _migrationsByFromVersion;

    public SettingsMigrator(IEnumerable<ISettingsMigration> migrations)
    {
        _migrationsByFromVersion = migrations.ToDictionary(migration => migration.FromVersion);
    }

    /// <summary>The migration chain this build ships with. Add new steps here as the
    /// schema grows; never remove or change an existing one once released.</summary>
    public static SettingsMigrator Default { get; } = new(
    [
        new AddSchemaVersionMigration(),
    ]);

    public MigrationResult Migrate(JsonObject document)
    {
        var version = ReadVersion(document);
        var migrated = false;

        while (version < SettingsSchema.CurrentVersion)
        {
            if (!_migrationsByFromVersion.TryGetValue(version, out var migration))
            {
                throw new SettingsFormatException(
                    $"Não existe migração de settings.json da versão {version} para a próxima.");
            }

            document = migration.Migrate(document);
            migrated = true;
            version = ReadVersion(document);
        }

        return new MigrationResult(document, migrated);
    }

    private static int ReadVersion(JsonObject document) =>
        document.TryGetPropertyValue("schemaVersion", out var node) && node is not null
            ? node.GetValue<int>()
            : 0;
}
