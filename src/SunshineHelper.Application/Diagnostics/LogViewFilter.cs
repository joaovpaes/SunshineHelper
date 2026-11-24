namespace SunshineHelper.Application.Diagnostics;

/// <summary>What the Log screen shows of app.log: every line, or only the lines the user
/// should look at (warnings and errors). Pure, so it is easy to test.</summary>
public static class LogViewFilter
{
    private const string WarningTag = "[WARNING]";
    private const string ErrorTag = "[ERROR]";

    /// <summary>True when the line was written at the warning or error level.</summary>
    public static bool IsProblem(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Contains(WarningTag, StringComparison.Ordinal)
            || line.Contains(ErrorTag, StringComparison.Ordinal);
    }

    /// <summary>The lines to show, oldest first. With <paramref name="onlyProblems"/> only
    /// warnings and errors are kept.</summary>
    public static IReadOnlyList<string> Apply(IReadOnlyList<string> lines, bool onlyProblems)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return onlyProblems ? lines.Where(IsProblem).ToList() : lines;
    }

    /// <summary>The lines joined for a text box or the clipboard, one per line.</summary>
    public static string Join(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return string.Join(Environment.NewLine, lines);
    }
}
