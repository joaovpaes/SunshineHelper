using System.Threading;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Settings;

/// <summary>Loads, validates, migrates and saves settings.json. See
/// docs/adr/0002-arquitetura-do-nucleo.md and docs/fase1-plano.md ("Configuração e
/// perfis"). File access and the clock are both injected so this class has no I/O of
/// its own and is unit-testable without touching disk.</summary>
public sealed class SettingsService
{
    private readonly ISettingsFileStore _fileStore;
    private readonly TimeProvider _timeProvider;

    public SettingsService(ISettingsFileStore fileStore)
        : this(fileStore, TimeProvider.System)
    {
    }

    public SettingsService(ISettingsFileStore fileStore, TimeProvider timeProvider)
    {
        _fileStore = fileStore;
        _timeProvider = timeProvider;
    }

    /// <summary>Reads settings.json, migrating and re-saving it first if its
    /// schemaVersion is out of date. A missing file is not an error: it returns
    /// <see cref="AppSettings.CreateDefault"/> with <c>Created</c> set, and nothing is
    /// written — the caller decides whether and when to create the file. A file that
    /// cannot be parsed or does not match the expected shape is not an error either:
    /// it comes back as defaults with the problem in <c>Validation</c>, so a corrupt
    /// settings.json never blocks the app from starting.</summary>
    public async Task<SettingsLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var text = await _fileStore.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        if (text is null)
        {
            return new SettingsLoadResult(AppSettings.CreateDefault(), SettingsValidationResult.Valid, Migrated: false, Created: true);
        }

        try
        {
            var raw = SettingsSerializer.ParseRaw(text);
            var migration = SettingsMigrator.Default.Migrate(raw);
            var settings = SettingsSerializer.Deserialize(migration.Document);
            var validation = SettingsValidator.Validate(settings);

            if (migration.Migrated && validation.IsValid)
            {
                await SaveAsync(path, settings, cancellationToken).ConfigureAwait(false);
            }

            return new SettingsLoadResult(settings, validation, migration.Migrated, Created: false);
        }
        catch (SettingsFormatException ex)
        {
            return new SettingsLoadResult(
                AppSettings.CreateDefault(),
                new SettingsValidationResult([ex.Message]),
                Migrated: false,
                Created: false);
        }
    }

    /// <summary>Backs up the current file (once per UTC day, never overwriting an
    /// earlier backup from the same day) and then writes the new content atomically.</summary>
    public async Task SaveAsync(string path, AppSettings settings, CancellationToken cancellationToken = default)
    {
        await BackupIfNeededAsync(path, cancellationToken).ConfigureAwait(false);

        var json = SettingsSerializer.Serialize(settings);
        await _fileStore.WriteAllTextAtomicAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task BackupIfNeededAsync(string path, CancellationToken cancellationToken)
    {
        if (!_fileStore.Exists(path))
        {
            return;
        }

        var backupPath = BuildBackupPath(path, _timeProvider.GetUtcNow());

        if (_fileStore.Exists(backupPath))
        {
            return;
        }

        await _fileStore.CopyAsync(path, backupPath, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildBackupPath(string path, DateTimeOffset asOf)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var backupDirectory = Path.Combine(directory, "backups");
        return Path.Combine(backupDirectory, $"{fileName}-{asOf:yyyyMMdd}{extension}");
    }
}
