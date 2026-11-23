using System.Globalization;

namespace SunshineHelper.Domain.Tests.Ui;

/// <summary>WCAG 2.x contrast ratio between two <c>#RRGGBB</c> colors — the script
/// docs/fase1-plano.md promised for checking the Sinal palette.</summary>
internal static class ContrastRatio
{
    public static double Between(string foreground, string background)
    {
        double a = Luminance(foreground);
        double b = Luminance(background);
        double lighter = Math.Max(a, b);
        double darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(string hex)
    {
        string digits = hex.TrimStart('#');
        double Channel(int start)
        {
            double c = int.Parse(digits.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }
}
