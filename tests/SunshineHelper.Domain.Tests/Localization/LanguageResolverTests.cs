using Shouldly;
using SunshineHelper.Domain.Localization;
using Xunit;

namespace SunshineHelper.Domain.Tests.Localization;

public class LanguageResolverTests
{
    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("PT_br", "pt-BR")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en", "en")]
    public void Resolve_sem_preferencia_segue_o_windows(string systemCulture, string expected)
    {
        LanguageResolver.Resolve(null, systemCulture).ShouldBe(expected);
        LanguageResolver.Resolve("", systemCulture).ShouldBe(expected);
        LanguageResolver.Resolve("  ", systemCulture).ShouldBe(expected);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("pte")]
    [InlineData("english")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_idioma_sem_tabela_cai_em_ingles(string? systemCulture)
    {
        LanguageResolver.Resolve(null, systemCulture).ShouldBe(LanguageResolver.English);
    }

    [Fact]
    public void Resolve_preferencia_do_usuario_vence_o_windows()
    {
        LanguageResolver.Resolve("en", "pt-BR").ShouldBe(LanguageResolver.English);
        LanguageResolver.Resolve("pt-BR", "en-US").ShouldBe(LanguageResolver.Portuguese);
    }

    [Fact]
    public void Resolve_preferencia_invalida_volta_para_o_windows()
    {
        // A settings.json edited by hand (or from a future version) naming a language this
        // build does not have must not leave the UI without text.
        LanguageResolver.Resolve("klingon", "pt-BR").ShouldBe(LanguageResolver.Portuguese);
        LanguageResolver.Resolve("klingon", "de-DE").ShouldBe(LanguageResolver.English);
    }

    [Fact]
    public void Supported_lista_exatamente_os_dois_idiomas_com_tabela()
    {
        string[] expected = [LanguageResolver.Portuguese, LanguageResolver.English];

        LanguageResolver.Supported.ShouldBe(expected);
    }
}
