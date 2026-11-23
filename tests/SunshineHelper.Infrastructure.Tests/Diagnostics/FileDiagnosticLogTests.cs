using Shouldly;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Infrastructure.Diagnostics;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Diagnostics;

public sealed class FileDiagnosticLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sunshinehelper-tests-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public void Write_cria_a_pasta_e_grava_uma_linha_com_nivel_e_area()
    {
        string path = Path.Combine(_directory, "logs", "app.log");
        var log = new FileDiagnosticLog(path);

        log.Write(DiagnosticLevel.Warning, "display", "mode changed");

        string[] lines = File.ReadAllLines(path);
        string line = lines.ShouldHaveSingleItem();
        line.ShouldContain("[WARNING] display: mode changed");
    }

    [Fact]
    public void Write_apaga_ip_e_nome_de_usuario_antes_de_gravar()
    {
        string path = Path.Combine(_directory, "app.log");
        var log = new FileDiagnosticLog(path);

        log.Write(DiagnosticLevel.Info, "channel", "from 192.168.5.146 using C:\\Users\\joaop\\x");

        string text = File.ReadAllText(path);
        text.ShouldContain("<ip>");
        text.ShouldContain("<user>");
        text.ShouldNotContain("192.168");
        text.ShouldNotContain("joaop");
    }

    [Fact]
    public void Write_mantem_cada_evento_em_uma_unica_linha()
    {
        string path = Path.Combine(_directory, "app.log");
        var log = new FileDiagnosticLog(path);

        log.Write(DiagnosticLevel.Error, "orchestrator", "first\r\nsecond");

        File.ReadAllLines(path).Length.ShouldBe(1);
    }

    [Fact]
    public void Write_passa_o_arquivo_para_app_log_1_ao_ultrapassar_o_limite()
    {
        string path = Path.Combine(_directory, "app.log");
        var log = new FileDiagnosticLog(path, maxBytes: 200);

        for (int i = 0; i < 20; i++)
        {
            log.Write(DiagnosticLevel.Info, "test", "a line long enough to fill the file quickly");
        }

        File.Exists(path + ".1").ShouldBeTrue();
        new FileInfo(path).Length.ShouldBeLessThan(400);
    }

    [Fact]
    public void Write_nao_lanca_quando_a_pasta_nao_pode_ser_criada()
    {
        Directory.CreateDirectory(_directory);
        string blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "a file where a folder is needed");
        var log = new FileDiagnosticLog(Path.Combine(blocker, "app.log"));

        Should.NotThrow(() => log.Write(DiagnosticLevel.Info, "test", "dropped"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
