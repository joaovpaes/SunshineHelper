using Shouldly;
using Xunit;

namespace SunshineHelper.Domain.Tests.Ui;

/// <summary>
/// The Sinal palette, checked as data (docs/adr/0005-casca-da-interface.md): both theme
/// files define exactly the same colors, and the text/state colors are readable on the
/// surfaces they are drawn on. Reads the real XAML, so editing a color in the app cannot
/// quietly break contrast.
/// </summary>
public class SinalThemeTests
{
    private const string Dark = "src/SunshineHelper.App/Themes/Colors.Dark.xaml";
    private const string Light = "src/SunshineHelper.App/Themes/Colors.Light.xaml";

    public static TheoryData<string> Themes => new() { Dark, Light };

    [Fact]
    public void Os_dois_temas_definem_as_mesmas_cores()
    {
        var dark = XamlDictionary.ReadTexts(Dark).Keys.Order().ToList();
        var light = XamlDictionary.ReadTexts(Light).Keys.Order().ToList();

        light.ShouldBe(dark, "um tema sem uma cor deixaria o elemento sem pincel ao trocar");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Texto_principal_passa_AAA_nas_superficies(string theme)
    {
        var c = XamlDictionary.ReadTexts(theme);

        foreach (var surface in new[] { "Color.Window", "Color.Band", "Color.Panel", "Color.PanelRaised" })
        {
            ContrastRatio.Between(c["Color.Text"], c[surface]).ShouldBeGreaterThanOrEqualTo(7.0, $"Text sobre {surface}");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Texto_secundario_e_estados_passam_AA_nas_superficies(string theme)
    {
        var c = XamlDictionary.ReadTexts(theme);
        string[] readable =
        [
            "Color.TextDim", "Color.AccentFocus", "Color.Ok", "Color.Warn", "Color.Danger",
        ];

        foreach (var surface in new[] { "Color.Window", "Color.Panel" })
        {
            foreach (var color in readable)
            {
                ContrastRatio.Between(c[color], c[surface]).ShouldBeGreaterThanOrEqualTo(4.5, $"{color} sobre {surface}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Texto_de_metadado_e_legivel_mas_nunca_essencial(string theme)
    {
        // TextMute is for metadata only (the design says "nunca texto essencial"), so the
        // bar is the 3:1 that WCAG sets for non-essential/large text, not 4.5:1.
        var c = XamlDictionary.ReadTexts(theme);

        ContrastRatio.Between(c["Color.TextMute"], c["Color.Window"]).ShouldBeGreaterThanOrEqualTo(3.0);
        ContrastRatio.Between(c["Color.TextMute"], c["Color.Panel"]).ShouldBeGreaterThanOrEqualTo(3.0);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Botao_primario_tem_texto_legivel(string theme)
    {
        var c = XamlDictionary.ReadTexts(theme);

        ContrastRatio.Between(c["Color.OnAccent"], c["Color.AccentStrong"]).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData("#FFFFFF", "#000000", 21.0)]
    [InlineData("#777777", "#FFFFFF", 4.48)]
    public void ContrastRatio_confere_valores_conhecidos_do_WCAG(string foreground, string background, double expected)
    {
        ContrastRatio.Between(foreground, background).ShouldBe(expected, 0.01);
    }
}
