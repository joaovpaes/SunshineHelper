using Shouldly;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Domain.Tests.Settings;

public class LogPathRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Campo_vazio_e_valido_e_desliga_o_leitor(string? input)
    {
        var result = LogPathRule.Check(input);

        result.IsValid.ShouldBeTrue();
        result.Path.ShouldBeNull();
    }

    [Theory]
    [InlineData(@"C:\Sunshine\config\sunshine.log")]
    [InlineData(@"D:\Jogos\Sunshine\sunshine.log")]
    [InlineData(@"\\servidor\pasta\sunshine.log")]
    public void Caminho_completo_e_aceito(string input)
    {
        var result = LogPathRule.Check(input);

        result.IsValid.ShouldBeTrue();
        result.Path.ShouldBe(input);
    }

    [Fact]
    public void Espacos_e_aspas_ao_redor_sao_removidos()
    {
        // "Copy as path" in Explorer puts the quotes there.
        var result = LogPathRule.Check("  \"C:\\Sunshine\\sunshine.log\"  ");

        result.IsValid.ShouldBeTrue();
        result.Path.ShouldBe(@"C:\Sunshine\sunshine.log");
    }

    [Theory]
    [InlineData("sunshine.log")]
    [InlineData(@"config\sunshine.log")]
    [InlineData(@"C:sunshine.log")]
    [InlineData(@"C:\Sunshine\")]
    [InlineData("C:\\Sun\0shine.log")]
    public void Caminho_relativo_de_pasta_ou_com_caractere_invalido_e_recusado(string input)
    {
        var result = LogPathRule.Check(input);

        result.IsValid.ShouldBeFalse();
        result.Path.ShouldBeNull();
    }
}
