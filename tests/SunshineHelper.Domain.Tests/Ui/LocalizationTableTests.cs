using Shouldly;
using Xunit;

namespace SunshineHelper.Domain.Tests.Ui;

/// <summary>The two string tables must define the same keys: a key missing from one
/// language would show up as empty text in the UI, and nothing else would notice.</summary>
public class LocalizationTableTests
{
    private const string Portuguese = "src/SunshineHelper.App/Localization/Strings.pt-BR.xaml";
    private const string English = "src/SunshineHelper.App/Localization/Strings.en.xaml";

    [Fact]
    public void As_duas_tabelas_tem_as_mesmas_chaves()
    {
        var pt = XamlDictionary.ReadTexts(Portuguese).Keys.Order().ToList();
        var en = XamlDictionary.ReadTexts(English).Keys.Order().ToList();

        en.ShouldBe(pt);
    }

    [Fact]
    public void Nenhuma_chave_fica_com_texto_vazio()
    {
        // ReadTexts already drops empty elements, so an empty one would show up as a
        // missing key against the other table; this also guards a file emptied wholesale.
        XamlDictionary.ReadTexts(Portuguese).ShouldNotBeEmpty();
        XamlDictionary.ReadTexts(English).ShouldNotBeEmpty();
    }

    [Fact]
    public void Toda_tela_da_navegacao_tem_titulo_e_descricao()
    {
        var pt = XamlDictionary.ReadTexts(Portuguese);

        foreach (var page in new[] { "Panel", "Profiles", "Devices", "Controller", "Log", "General" })
        {
            pt.ShouldContainKey($"Nav.{page}");
            pt.ShouldContainKey($"Page.{page}.Title");
            pt.ShouldContainKey($"Page.{page}.Description");
        }
    }

    [Fact]
    public void Todo_botao_do_controle_tem_nome_nos_dois_idiomas()
    {
        // The Controller screen names a button by "Button.<enum name>".
        var pt = XamlDictionary.ReadTexts(Portuguese);
        var en = XamlDictionary.ReadTexts(English);

        foreach (var name in Enum.GetNames<SunshineHelper.Domain.Gamepad.GamepadButtons>().Where(n => n != "None"))
        {
            pt.ShouldContainKey($"Button.{name}");
            en.ShouldContainKey($"Button.{name}");
        }
    }

    [Theory]
    [InlineData("TooFewButtons")]
    [InlineData("TooManyButtons")]
    [InlineData("StartAndBack")]
    [InlineData("SameAsOther")]
    [InlineData("HoldTooShort")]
    [InlineData("HoldTooLong")]
    public void Todo_aviso_de_combo_tem_texto_nos_dois_idiomas(string issue)
    {
        // The Controller screen names a warning by "Controller.Issue.<ComboIssue name>".
        XamlDictionary.ReadTexts(Portuguese).ShouldContainKey($"Controller.Issue.{issue}");
        XamlDictionary.ReadTexts(English).ShouldContainKey($"Controller.Issue.{issue}");
    }
}
