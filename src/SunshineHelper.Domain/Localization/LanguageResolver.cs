namespace SunshineHelper.Domain.Localization;

/// <summary>
/// Picks which UI language to show (docs/adr/0005-casca-da-interface.md). Pure, so the
/// rules are testable without WPF: the app only has two string tables, so every culture
/// the system or the user names has to land on one of them.
/// </summary>
public static class LanguageResolver
{
    public const string Portuguese = "pt-BR";

    public const string English = "en";

    /// <summary>The languages that have a string table in the app.</summary>
    public static IReadOnlyList<string> Supported { get; } = [Portuguese, English];

    /// <summary>
    /// <paramref name="preference"/> is what the user chose in settings: null or empty
    /// means "follow Windows", otherwise a language name such as <c>pt-BR</c> or
    /// <c>en</c>. <paramref name="systemCulture"/> is the Windows UI culture name
    /// (<c>pt-PT</c>, <c>en-GB</c>, <c>de-DE</c> ...). Anything not Portuguese or English
    /// ends up in English, the language a stranger is most likely to be able to read.
    /// </summary>
    public static string Resolve(string? preference, string? systemCulture)
    {
        if (!string.IsNullOrWhiteSpace(preference))
        {
            return Match(preference) ?? Match(systemCulture) ?? English;
        }

        return Match(systemCulture) ?? English;
    }

    private static string? Match(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        // Any Portuguese (pt, pt-PT, pt-BR) reads the pt-BR table: the app has no separate
        // European Portuguese text, and it is far closer to that than to English.
        if (cultureName.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            && (cultureName.Length == 2 || cultureName[2] is '-' or '_'))
        {
            return Portuguese;
        }

        if (cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            && (cultureName.Length == 2 || cultureName[2] is '-' or '_'))
        {
            return English;
        }

        return null;
    }
}
