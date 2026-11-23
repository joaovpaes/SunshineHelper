namespace SunshineHelper.Domain.Monitoring;

/// <summary>What a sample of Sunshine's log says about whether it is detailed enough.</summary>
public enum LogLevelVerdict
{
    /// <summary>Nothing to judge from (empty or unreadable sample): say nothing.</summary>
    Unknown,

    /// <summary>The log level includes Debug, where <c>/cancel</c>, <c>/resume</c> and
    /// <c>Session ended</c> are written.</summary>
    Sufficient,

    /// <summary>Those lines would never appear: the log reader cannot see the end of a session.</summary>
    Insufficient,
}

/// <summary>
/// Decides whether Sunshine writes the lines the log reader depends on. Those lines exist
/// only at Debug level (docs/adr/0004-leitor-do-log.md). The app never changes Sunshine's
/// configuration; it only tells the user. The setting itself wins when Sunshine printed
/// it; otherwise the presence of any Debug line proves the level is enough.
/// </summary>
public static class SunshineLogLevelCheck
{
    private const string ConfigKey = "min_log_level";
    private const string DebugMarker = ": Debug:";

    // Sunshine's numbering: 0 verbose, 1 debug, 2 info, 3 warning, 4 error, 5 fatal, 6 none.
    private const int HighestSufficientLevel = 1;

    public static LogLevelVerdict Evaluate(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        bool sawAnyLine = false;
        bool sawDebug = false;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            sawAnyLine = true;

            if (TryReadConfiguredLevel(line, out bool sufficient))
            {
                return sufficient ? LogLevelVerdict.Sufficient : LogLevelVerdict.Insufficient;
            }

            sawDebug |= line.Contains(DebugMarker, StringComparison.Ordinal);
        }

        if (!sawAnyLine)
        {
            return LogLevelVerdict.Unknown;
        }

        return sawDebug ? LogLevelVerdict.Sufficient : LogLevelVerdict.Insufficient;
    }

    /// <summary>Reads <c>min_log_level = 1</c> or <c>min_log_level = debug</c> (any case).</summary>
    private static bool TryReadConfiguredLevel(string line, out bool sufficient)
    {
        sufficient = false;
        int at = line.IndexOf(ConfigKey, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return false;
        }

        var rest = line.AsSpan(at + ConfigKey.Length).TrimStart(" \t=:\"'");
        int end = 0;
        while (end < rest.Length && char.IsLetterOrDigit(rest[end]))
        {
            end++;
        }

        var token = rest[..end];
        if (token.IsEmpty)
        {
            return false;
        }

        if (int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int level))
        {
            sufficient = level <= HighestSufficientLevel;
            return true;
        }

        if (token.Equals("debug", StringComparison.OrdinalIgnoreCase) || token.Equals("verbose", StringComparison.OrdinalIgnoreCase))
        {
            sufficient = true;
            return true;
        }

        if (token.Equals("info", StringComparison.OrdinalIgnoreCase)
            || token.Equals("warning", StringComparison.OrdinalIgnoreCase)
            || token.Equals("error", StringComparison.OrdinalIgnoreCase)
            || token.Equals("fatal", StringComparison.OrdinalIgnoreCase)
            || token.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
