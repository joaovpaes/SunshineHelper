namespace SunshineHelper.Domain.Settings;

/// <summary>The whole contents of settings.json, after migration to
/// <see cref="SettingsSchema.CurrentVersion"/>.</summary>
public sealed record AppSettings
{
    public required int SchemaVersion { get; init; }

    /// <summary>Profile used when no match rule fires. Null means "ask on the first
    /// connection and remember the answer" (docs/adr/0002-arquitetura-do-nucleo.md,
    /// "Escolha do perfil", item 4 — not implemented yet; the field exists so
    /// settings.json stays forward-compatible).</summary>
    public string? DefaultProfileId { get; init; }

    public TimingSettings Timing { get; init; } = new();

    public IReadOnlyList<ClientProfile> Profiles { get; init; } = [];

    /// <summary>Full path to Sunshine's own log file, read by the log reader
    /// (docs/adr/0004-leitor-do-log.md). Null until the user sets it in the UI (Phase 1,
    /// PR "Casca da interface") — there is no install path common to every Sunshine
    /// installation to default to, and this app must not assume the author's own.</summary>
    public string? SunshineLogPath { get; init; }

    /// <summary>The launcher closing policy (docs/adr/0008-modulo-launcher.md). Optional in
    /// the file: older settings.json load with no launcher configured.</summary>
    public LauncherSettings Launcher { get; init; } = new();

    /// <summary>UI language the user chose in the General screen: <c>pt-BR</c> or
    /// <c>en</c>. Null follows Windows (<see cref="Localization.LanguageResolver"/>); an
    /// unknown value in a hand-edited file is not an error, the resolver falls back.
    /// Optional, so older files load unchanged — no schema bump.</summary>
    public string? Language { get; init; }

    /// <summary>What a brand-new installation starts with: no profiles yet, schema
    /// at the current version, default timing. The UI (Phase 1, PR "Casca da
    /// interface") is where the user actually sets up their first profile.</summary>
    public static AppSettings CreateDefault() => new()
    {
        SchemaVersion = SettingsSchema.CurrentVersion,
        DefaultProfileId = null,
        Timing = new TimingSettings(),
        Profiles = [],
        SunshineLogPath = null,
        Language = null,
        Launcher = new LauncherSettings(),
    };
}
