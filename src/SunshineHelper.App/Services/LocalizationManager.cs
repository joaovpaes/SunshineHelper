using System.Globalization;
using SunshineHelper.Domain.Localization;

namespace SunshineHelper.App.Services;

/// <summary>
/// Loads the string table for the resolved language and swaps it live
/// (docs/adr/0005-casca-da-interface.md). The choice itself is
/// <see cref="LanguageResolver"/>'s, which is unit tested; this class only loads the file.
/// </summary>
internal sealed class LocalizationManager(DictionarySlot slot)
{
    /// <summary>The language now shown: <c>pt-BR</c> or <c>en</c>.</summary>
    public string Current { get; private set; } = LanguageResolver.English;

    /// <param name="preference">Null follows Windows' UI language.</param>
    public void Apply(string? preference = null)
    {
        Current = LanguageResolver.Resolve(preference, CultureInfo.CurrentUICulture.Name);
        slot.Set(new Uri($"pack://application:,,,/SunshineHelper;component/Localization/Strings.{Current}.xaml"));
    }
}
