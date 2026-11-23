namespace SunshineHelper.Domain.Settings;

/// <summary>What the user typed as the Sunshine log path, cleaned up, and whether it can
/// be used. <see cref="Path"/> is null when the field was left empty (log reader off).</summary>
public sealed record LogPathCheck(string? Path, bool IsValid);

/// <summary>
/// Checks the Sunshine log path typed in the General screen. Pure string rules only: the
/// file itself may legitimately not exist yet (the log reader waits for it), so existence
/// is never required here. A relative path is refused because the app's working directory
/// is not something the user controls — "sunshine.log" would silently point somewhere
/// different depending on how the app was started.
/// </summary>
public static class LogPathRule
{
    public static LogPathCheck Check(string? input)
    {
        string trimmed = (input ?? string.Empty).Trim().Trim('"');

        if (trimmed.Length == 0)
        {
            return new LogPathCheck(null, IsValid: true);
        }

        bool usable = trimmed.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0
            && System.IO.Path.IsPathFullyQualified(trimmed)
            && !trimmed.EndsWith('\\')
            && !trimmed.EndsWith('/');

        return usable ? new LogPathCheck(trimmed, IsValid: true) : new LogPathCheck(null, IsValid: false);
    }
}
