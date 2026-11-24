using Shouldly;
using SunshineHelper.Application.Diagnostics;
using Xunit;

namespace SunshineHelper.Application.Tests.Diagnostics;

public class LogViewFilterTests
{
    private static readonly string[] _lines =
    [
        "2026-11-23 14:07:27.644 [INFO] channel: request received",
        "2026-11-23 14:07:30.900 [WARNING] display: mode changed",
        "2026-11-23 15:49:42.156 [ERROR] orchestrator: apply module 'fps': Failed",
        "2026-11-23 15:49:42.222 [INFO] orchestrator: apply module 'launcher': Changed",
    ];

    [Theory]
    [InlineData("2026-11-23 14:07:27.644 [INFO] channel: request received", false)]
    [InlineData("2026-11-23 14:07:30.900 [WARNING] display: mode changed", true)]
    [InlineData("2026-11-23 15:49:42.156 [ERROR] orchestrator: failed", true)]
    [InlineData("", false)]
    public void IsProblem_reconhece_aviso_e_erro(string line, bool expected)
    {
        LogViewFilter.IsProblem(line).ShouldBe(expected);
    }

    [Fact]
    public void Apply_sem_filtro_devolve_todas_as_linhas()
    {
        LogViewFilter.Apply(_lines, onlyProblems: false).Count.ShouldBe(4);
    }

    [Fact]
    public void Apply_com_filtro_devolve_so_avisos_e_erros_na_mesma_ordem()
    {
        var result = LogViewFilter.Apply(_lines, onlyProblems: true);

        result.Count.ShouldBe(2);
        result[0].ShouldContain("[WARNING]");
        result[1].ShouldContain("[ERROR]");
    }

    [Fact]
    public void Apply_com_lista_vazia_devolve_lista_vazia()
    {
        LogViewFilter.Apply([], onlyProblems: true).ShouldBeEmpty();
    }

    [Fact]
    public void Join_poe_uma_linha_por_linha()
    {
        string text = LogViewFilter.Join(["a", "b"]);

        text.ShouldBe("a" + Environment.NewLine + "b");
        LogViewFilter.Join([]).ShouldBeEmpty();
    }

    [Fact]
    public void Metodos_recusam_argumento_nulo()
    {
        Should.Throw<ArgumentNullException>(() => LogViewFilter.IsProblem(null!));
        Should.Throw<ArgumentNullException>(() => LogViewFilter.Apply(null!, false));
        Should.Throw<ArgumentNullException>(() => LogViewFilter.Join(null!));
    }
}
