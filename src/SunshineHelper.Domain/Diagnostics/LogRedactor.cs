using System.Text.RegularExpressions;

namespace SunshineHelper.Domain.Diagnostics;

/// <summary>
/// Removes what must never leave the PC from a line of text before it is written to the
/// app's own log or to a diagnostic report: IP addresses, MAC addresses, Sunshine's
/// <c>rikey</c> and <c>uniqueid</c>, and the Windows account name inside a profile path.
/// Pure, so the rules are testable on their own.
/// </summary>
public static partial class LogRedactor
{
    public const string IpPlaceholder = "<ip>";
    public const string MacPlaceholder = "<mac>";
    public const string SecretPlaceholder = "<redacted>";
    public const string UserPlaceholder = "<user>";

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string result = SecretPattern().Replace(text, match => match.Groups["key"].Value + match.Groups["sep"].Value + SecretPlaceholder);
        result = MacPattern().Replace(result, MacPlaceholder);
        result = Ipv4Pattern().Replace(result, IpPlaceholder);
        result = UserPathPattern().Replace(result, match => match.Groups["prefix"].Value + UserPlaceholder);
        return result;
    }

    [GeneratedRegex(@"(?<key>rikey|uniqueid|rikeyid)(?<sep>\s*[=:]\s*|"":\s*""|\s+--\s+)[^\s&"",;]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"\b[0-9A-Fa-f]{2}([:-])(?:[0-9A-Fa-f]{2}\1){4}[0-9A-Fa-f]{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex MacPattern();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Pattern();

    [GeneratedRegex(@"(?<prefix>[A-Za-z]:[\\/]+Users[\\/]+)[^\\/""'\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserPathPattern();
}
