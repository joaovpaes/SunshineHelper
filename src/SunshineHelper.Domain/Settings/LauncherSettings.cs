namespace SunshineHelper.Domain.Settings;

/// <summary>
/// Which programs count as "the launcher" Sunshine opens for a stream (Playnite, for
/// instance) and how long to wait for them to close politely at the end of a session
/// (docs/adr/0008-modulo-launcher.md). Global, not per profile: the launcher belongs to
/// the Sunshine app being streamed, not to the client watching it.
/// </summary>
public sealed record LauncherSettings
{
    /// <summary>Process names as Windows lists them, without <c>.exe</c> (for example
    /// <c>Playnite.FullscreenApp</c>). Empty — the default — means the app never closes
    /// anything. Nothing is assumed about which launcher the user has.</summary>
    public IReadOnlyList<string> ProcessNames { get; init; } = [];

    /// <summary>How long a launcher gets to close after being asked to, before it and its
    /// child processes are ended.</summary>
    public int CloseTimeoutMs { get; init; } = 10_000;

    /// <summary>Names never accepted in <see cref="ProcessNames"/>: ending them would take
    /// down Windows' shell or session, Sunshine itself (which is still running the stream
    /// teardown) or this app. Compared after <see cref="NormalizeName"/>, ignoring case.</summary>
    public static IReadOnlySet<string> ForbiddenNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SunshineHelper",
        "SunshineHelper.Trigger",
        "sunshine",
        "sunshinesvc",
        "explorer",
        "dwm",
        "csrss",
        "wininit",
        "winlogon",
        "lsass",
        "services",
        "smss",
        "svchost",
        "System",
        "Idle",
        "Registry",
        "fontdrvhost",
        "conhost",
    };

    /// <summary>The longest launcher delay accepted. The delay holds Sunshine's Prep Command,
    /// and the auxiliary stops waiting well before a minute (ADR 0002, "Orçamento de tempo").</summary>
    public const int MaxDelayMs = 30_000;

    /// <summary>Turns what the user typed in the settings box — one name per line, commas or
    /// semicolons also accepted — into the list stored in settings.json: trimmed, without
    /// <c>.exe</c>, empty entries dropped, no repeats.</summary>
    public static IReadOnlyList<string> ParseNames(string? text) =>
        (text ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The form names are compared in: trimmed, with a trailing <c>.exe</c>
    /// dropped, because that is what users copy from Task Manager and what
    /// <c>Process.ProcessName</c> leaves off.</summary>
    public static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4].TrimEnd()
            : trimmed;
    }
}
