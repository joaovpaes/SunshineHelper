using System.Globalization;
using Shouldly;
using SunshineHelper.Application.Diagnostics;
using Xunit;

namespace SunshineHelper.Application.Tests.Diagnostics;

public class DiagnosticReportBuilderTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 23, 14, 30, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Build_traz_versao_descricao_configuracoes_e_logs()
    {
        var report = DiagnosticReportBuilder.Build(Input() with
        {
            Description = "  A resolução não muda.  ",
            SettingsJson = "{ \"schemaVersion\": 1 }",
            StartupProblems = ["settings ignored"],
            AppLogLines = ["app line"],
            SunshineLogPath = "D:\\Sunshine\\sunshine.log",
            SunshineLogLines = ["sunshine line"],
        });

        report.ShouldContain("# SunshineHelper diagnostic report");
        report.ShouldContain("Generated: 2026-11-23 14:30:00 -03:00");
        report.ShouldContain("App version: 1.0.0");
        report.ShouldContain("Windows version: 10.0.26300.0");
        report.ShouldContain("## Problem description\nA resolução não muda.\n");
        report.ShouldContain("- settings ignored");
        report.ShouldContain("{ \"schemaVersion\": 1 }");
        report.ShouldContain("## App log (last 1 lines)");
        report.ShouldContain("app line");
        report.ShouldContain("## Sunshine log (last 1 lines of D:\\Sunshine\\sunshine.log)");
        report.ShouldContain("sunshine line");
    }

    [Fact]
    public void Build_nao_transforma_a_versao_do_app_nem_a_do_windows_em_ip()
    {
        var report = DiagnosticReportBuilder.Build(Input());

        report.ShouldNotContain("<ip>");
    }

    [Fact]
    public void Build_apaga_ip_mac_chaves_e_usuario_em_todas_as_secoes()
    {
        var report = DiagnosticReportBuilder.Build(Input() with
        {
            Description = "Cliente em 192.168.5.146 não conecta",
            SettingsJson = "{ \"sunshineLogPath\": \"C:\\\\Users\\\\joaop\\\\sunshine.log\" }",
            StartupProblems = ["problema com 10.1.2.3"],
            AppLogLines = ["device 00:1A:2B:3C:4D:5E"],
            SunshineLogPath = "C:\\Users\\joaop\\sunshine.log",
            SunshineLogLines = ["rikey=ABCDEF0123456789 uniqueid -- 0123456789ABCDEF"],
        });

        report.ShouldNotContain("192.168");
        report.ShouldNotContain("10.1.2.3");
        report.ShouldNotContain("00:1A:2B");
        report.ShouldNotContain("joaop");
        report.ShouldNotContain("ABCDEF0123456789");
        report.ShouldNotContain("0123456789ABCDEF");
        report.ShouldContain("<ip>");
        report.ShouldContain("<mac>");
        report.ShouldContain("<user>");
        report.ShouldContain("<redacted>");
    }

    [Fact]
    public void Build_guarda_so_o_fim_de_cada_log()
    {
        string[] appLines = [.. Enumerable.Range(1, DiagnosticReportBuilder.AppLogLineLimit + 50).Select(i => "app-" + i.ToString("D5", CultureInfo.InvariantCulture))];
        string[] sunshineLines = [.. Enumerable.Range(1, DiagnosticReportBuilder.SunshineLogLineLimit + 50).Select(i => "sun-" + i.ToString("D5", CultureInfo.InvariantCulture))];

        var report = DiagnosticReportBuilder.Build(Input() with
        {
            AppLogLines = appLines,
            SunshineLogPath = "x.log",
            SunshineLogLines = sunshineLines,
        });

        report.ShouldNotContain("app-00001");
        report.ShouldNotContain("app-00050\n");
        report.ShouldContain("app-00051");
        report.ShouldContain("app-" + (DiagnosticReportBuilder.AppLogLineLimit + 50).ToString("D5", CultureInfo.InvariantCulture));
        report.ShouldNotContain("sun-00050\n");
        report.ShouldContain("sun-00051");
        report.ShouldContain("(last " + DiagnosticReportBuilder.AppLogLineLimit.ToString(CultureInfo.InvariantCulture) + " lines)");
    }

    [Fact]
    public void Build_avisa_quando_algo_nao_esta_disponivel()
    {
        var report = DiagnosticReportBuilder.Build(Input() with
        {
            SettingsJson = null,
            AppLogLines = null,
            SunshineLogPath = "D:\\Sunshine\\sunshine.log",
            SunshineLogLines = null,
        });

        report.ShouldContain("## Problem description\n(none)\n");
        report.ShouldContain("## Startup problems\n(none)\n");
        report.ShouldContain("(not available: file missing or unreadable)");
        report.ShouldContain("(not available: app.log missing or unreadable)");
    }

    [Fact]
    public void Build_diz_que_o_log_do_sunshine_nao_foi_configurado()
    {
        var report = DiagnosticReportBuilder.Build(Input() with { SunshineLogPath = "  " });

        report.ShouldContain("## Sunshine log\n(not configured");
    }

    private static DiagnosticReportInput Input() => new()
    {
        GeneratedAt = Moment,
        AppVersion = "1.0.0",
        OsVersion = "10.0.26300.0",
    };
}
