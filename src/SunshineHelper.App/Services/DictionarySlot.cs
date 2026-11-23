using System.Windows;

namespace SunshineHelper.App.Services;

/// <summary>
/// One place in <see cref="ResourceDictionary.MergedDictionaries"/> that holds exactly one
/// of several interchangeable dictionaries (a theme, a language). Swapping replaces the
/// previous one, so every <c>DynamicResource</c> bound to its keys updates live.
/// </summary>
internal sealed class DictionarySlot(ResourceDictionary host)
{
    private ResourceDictionary? _current;

    public void Set(Uri source)
    {
        var next = new ResourceDictionary { Source = source };

        if (_current is not null)
        {
            host.MergedDictionaries.Remove(_current);
        }

        host.MergedDictionaries.Add(next);
        _current = next;
    }
}
