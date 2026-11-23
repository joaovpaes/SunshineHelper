using SunshineHelper.Domain.Monitoring;

namespace SunshineHelper.Domain.Tests.Monitoring;

public sealed class SunshineLogLevelCheckTests
{
    [Theory]
    [InlineData("min_log_level = 0", LogLevelVerdict.Sufficient)]
    [InlineData("min_log_level = 1", LogLevelVerdict.Sufficient)]
    [InlineData("min_log_level = debug", LogLevelVerdict.Sufficient)]
    [InlineData("[2026-11-22 09:00:00.000]: Info: min_log_level = Debug", LogLevelVerdict.Sufficient)]
    [InlineData("min_log_level = 2", LogLevelVerdict.Insufficient)]
    [InlineData("min_log_level = info", LogLevelVerdict.Insufficient)]
    [InlineData("min_log_level = WARNING", LogLevelVerdict.Insufficient)]
    [InlineData("min_log_level = 5", LogLevelVerdict.Insufficient)]
    public void A_configuracao_impressa_decide(string line, LogLevelVerdict expected)
    {
        var lines = new List<string> { line };

        SunshineLogLevelCheck.Evaluate(lines).ShouldBe(expected);
    }

    [Fact]
    public void A_configuracao_vale_mais_que_as_linhas_de_debug()
    {
        var lines = new List<string> { "[t]: Debug: DESTINATION :: /serverinfo", "min_log_level = 3" };

        SunshineLogLevelCheck.Evaluate(lines).ShouldBe(LogLevelVerdict.Insufficient);
    }

    [Fact]
    public void Sem_configuracao_uma_linha_de_debug_basta()
    {
        var lines = new List<string> { "[t]: Info: CLIENT CONNECTED", "[t]: Debug: DESTINATION :: /cancel" };

        SunshineLogLevelCheck.Evaluate(lines).ShouldBe(LogLevelVerdict.Sufficient);
    }

    [Fact]
    public void Sem_configuracao_e_sem_debug_o_nivel_e_insuficiente()
    {
        var lines = new List<string> { "[t]: Info: CLIENT CONNECTED", "[t]: Info: CLIENT DISCONNECTED" };

        SunshineLogLevelCheck.Evaluate(lines).ShouldBe(LogLevelVerdict.Insufficient);
    }

    [Fact]
    public void Amostra_vazia_ou_em_branco_nao_diz_nada()
    {
        var none = new List<string>();
        var blanks = new List<string> { string.Empty, "   " };

        SunshineLogLevelCheck.Evaluate(none).ShouldBe(LogLevelVerdict.Unknown);
        SunshineLogLevelCheck.Evaluate(blanks).ShouldBe(LogLevelVerdict.Unknown);
    }

    [Fact]
    public void Chave_sem_valor_legivel_e_ignorada()
    {
        var lines = new List<string> { "min_log_level = ???", "[t]: Debug: uuid -- x" };

        SunshineLogLevelCheck.Evaluate(lines).ShouldBe(LogLevelVerdict.Sufficient);
    }
}
