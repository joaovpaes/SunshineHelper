using System.Globalization;
using System.Text;
using SunshineHelper.Domain.Diagnostics;

namespace SunshineHelper.Application.Diagnostics;

/// <summary>Everything a diagnostic report is made of. Plain data, already read from disk
/// by the caller, so the builder stays pure and testable.</summary>
public sealed record DiagnosticReportInput
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required string AppVersion { get; init; }

    public required string OsVersion { get; init; }

    /// <summary>What the user typed about the problem.</summary>
    public string? Description { get; init; }

    /// <summary>The raw text of settings.json, or null when it could not be read.</summary>
    public string? SettingsJson { get; init; }

    public IReadOnlyList<string> StartupProblems { get; init; } = [];

    /// <summary>Lines of the app's own log, oldest first; null when it could not be read.</summary>
    public IReadOnlyList<string>? AppLogLines { get; init; }

    /// <summary>The configured Sunshine log path, or null when none is set.</summary>
    public string? SunshineLogPath { get; init; }

    /// <summary>Lines of the Sunshine log, oldest first; null when it could not be read.</summary>
    public IReadOnlyList<string>? SunshineLogLines { get; init; }
}

/// <summary>
/// Builds the text of the "diagnostic report" the user can hand to another tool or person:
/// the problem description, the settings, the startup problems and the end of the app log
/// and of the Sunshine log. The whole text goes through <see cref="LogRedactor"/> at the end,
/// description included, so nothing the user typed or a log contained can leak an address
/// or a key. Nothing is sent anywhere: the caller only writes the text to a file.
/// </summary>
public static class DiagnosticReportBuilder
{
    public const int AppLogLineLimit = 400;
    public const int SunshineLogLineLimit = 1500;

    public static string Build(DiagnosticReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var text = new StringBuilder();
        Line(text, "# SunshineHelper diagnostic report");
        Line(text, string.Create(CultureInfo.InvariantCulture, $"Generated: {input.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}"));
        Line(text, "App version: " + input.AppVersion);
        Line(text, "Windows version: " + input.OsVersion);
        Line(text, "Privacy: IP and MAC addresses, Sunshine pairing keys and the Windows user name were replaced by placeholders. Read the file before sharing it.");

        Section(text, "Problem description");
        Line(text, string.IsNullOrWhiteSpace(input.Description) ? "(none)" : input.Description.Trim());

        Section(text, "Startup problems");
        if (input.StartupProblems.Count == 0)
        {
            Line(text, "(none)");
        }
        else
        {
            foreach (string problem in input.StartupProblems)
            {
                Line(text, "- " + problem);
            }
        }

        Section(text, "settings.json");
        IReadOnlyList<string>? settingsLines = null;
        if (input.SettingsJson is not null)
        {
            settingsLines = [input.SettingsJson.TrimEnd()];
        }

        Block(text, settingsLines, "(not available: file missing or unreadable)");

        var appLines = Tail(input.AppLogLines, AppLogLineLimit);
        Section(text, string.Create(CultureInfo.InvariantCulture, $"App log (last {appLines?.Count ?? 0} lines)"));
        Block(text, appLines, "(not available: app.log missing or unreadable)");

        var sunshineLines = Tail(input.SunshineLogLines, SunshineLogLineLimit);
        if (string.IsNullOrWhiteSpace(input.SunshineLogPath))
        {
            Section(text, "Sunshine log");
            Line(text, "(not configured: no Sunshine log path is set in the app)");
        }
        else
        {
            Section(text, string.Create(CultureInfo.InvariantCulture, $"Sunshine log (last {sunshineLines?.Count ?? 0} lines of {input.SunshineLogPath})"));
            Block(text, sunshineLines, "(not available: file missing or unreadable)");
        }

        return LogRedactor.Redact(text.ToString());
    }

    private static IReadOnlyList<string>? Tail(IReadOnlyList<string>? lines, int limit) =>
        lines is null || lines.Count <= limit ? lines : [.. lines.Skip(lines.Count - limit)];

    private static void Section(StringBuilder text, string title)
    {
        text.Append('\n');
        Line(text, "## " + title);
    }

    private static void Block(StringBuilder text, IReadOnlyList<string>? lines, string whenMissing)
    {
        if (lines is null)
        {
            Line(text, whenMissing);
            return;
        }

        Line(text, "```");
        foreach (string line in lines)
        {
            Line(text, line);
        }

        Line(text, "```");
    }

    // Always "\n", whatever the platform, so the report looks the same wherever it was built.
    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
