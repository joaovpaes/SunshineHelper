using Shouldly;
using SunshineHelper.Infrastructure.Diagnostics;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Diagnostics;

public sealed class DiagnosticFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sunshinehelper-tests-" + Guid.NewGuid().ToString("n"));

    public DiagnosticFilesTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ReadLastLines_devolve_o_fim_do_arquivo_na_ordem_original()
    {
        string path = Write("a.log", "one\r\ntwo\r\n\r\nthree\r\nfour\r\n");

        string[] expected = ["three", "four"];

        DiagnosticFiles.ReadLastLines(path, maxLines: 2).ShouldNotBeNull().ToArray().ShouldBe(expected);
    }

    [Fact]
    public void ReadLastLines_devolve_tudo_quando_o_arquivo_e_curto()
    {
        string path = Write("a.log", "one\ntwo\n");

        string[] expected = ["one", "two"];

        DiagnosticFiles.ReadLastLines(path, maxLines: 10).ShouldNotBeNull().ToArray().ShouldBe(expected);
    }

    [Fact]
    public void ReadLastLines_descarta_a_primeira_linha_cortada_quando_so_le_o_fim()
    {
        string path = Write("a.log", "aaaaaaaaaa\nbbbbbbbbbb\ncccccccccc\n");

        // 15 bytes from the end start inside the second line.
        var lines = DiagnosticFiles.ReadLastLines(path, maxLines: 10, maxBytes: 15);

        lines.ShouldNotBeNull().ShouldHaveSingleItem().ShouldBe("cccccccccc");
    }

    [Fact]
    public void ReadLastLines_le_um_arquivo_que_outro_programa_mantem_aberto()
    {
        string path = Write("open.log", "one\ntwo\n");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        string[] expected = ["one", "two"];

        DiagnosticFiles.ReadLastLines(path, maxLines: 5).ShouldNotBeNull().ToArray().ShouldBe(expected);
    }

    [Fact]
    public void ReadLastLines_devolve_null_quando_o_arquivo_nao_existe()
    {
        DiagnosticFiles.ReadLastLines(Path.Combine(_directory, "missing.log"), maxLines: 5).ShouldBeNull();
    }

    [Fact]
    public void ReadText_devolve_o_conteudo_e_corta_no_limite()
    {
        string path = Write("settings.json", "{ \"a\": 1 }");

        DiagnosticFiles.ReadText(path).ShouldBe("{ \"a\": 1 }");
        DiagnosticFiles.ReadText(path, maxBytes: 3).ShouldBe("{ \"");
    }

    [Fact]
    public void ReadText_devolve_null_quando_o_arquivo_nao_existe()
    {
        DiagnosticFiles.ReadText(Path.Combine(_directory, "missing.json")).ShouldBeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string Write(string name, string content)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
