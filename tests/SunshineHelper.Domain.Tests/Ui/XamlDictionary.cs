using System.Xml.Linq;
using SunshineHelper.Domain.Tests.TestSupport;

namespace SunshineHelper.Domain.Tests.Ui;

/// <summary>A WPF ResourceDictionary read as plain XML: key to text content.</summary>
internal static class XamlDictionary
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Every element carrying an <c>x:Key</c> that is a leaf with text, such as
    /// <c>&lt;Color x:Key="Color.Text"&gt;#E8ECF4&lt;/Color&gt;</c>. Brushes (attributes only)
    /// have no text and are left out.</summary>
    public static Dictionary<string, string> ReadTexts(string relativePath)
    {
        var root = XDocument.Parse(RepoFiles.ReadText(relativePath)).Root
            ?? throw new InvalidOperationException($"{relativePath} está vazio.");

        return root.Elements()
            .Where(e => e.Attribute(_x + "Key") is not null && !e.HasElements && e.Value.Length > 0)
            .ToDictionary(e => (string)e.Attribute(_x + "Key")!, e => e.Value.Trim());
    }
}
