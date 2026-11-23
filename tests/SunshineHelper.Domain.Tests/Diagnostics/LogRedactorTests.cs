using Shouldly;
using SunshineHelper.Domain.Diagnostics;
using Xunit;

namespace SunshineHelper.Domain.Tests.Diagnostics;

public class LogRedactorTests
{
    [Fact]
    public void Redact_troca_enderecos_ip()
    {
        string result = LogRedactor.Redact("RAISE: 192.168.5.146:43597 :: VIDEO");

        result.ShouldBe("RAISE: <ip>:43597 :: VIDEO");
    }

    [Theory]
    [InlineData("device 00:1A:2b:3C:4d:5E ok", "device <mac> ok")]
    [InlineData("device 00-1A-2B-3C-4D-5E ok", "device <mac> ok")]
    public void Redact_troca_enderecos_mac(string input, string expected)
    {
        LogRedactor.Redact(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("rikey=ABCDEF0123456789&x=1", "rikey=<redacted>&x=1")]
    [InlineData("uniqueid -- 0123456789ABCDEF", "uniqueid -- <redacted>")]
    [InlineData("\"uniqueid\": \"0123456789ABCDEF\"", "\"uniqueid\": \"<redacted>\"")]
    [InlineData("RIKEY=abc", "RIKEY=<redacted>")]
    public void Redact_apaga_os_segredos_do_sunshine(string input, string expected)
    {
        LogRedactor.Redact(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("C:\\Users\\joaop\\OneDrive\\x.exe", "C:\\Users\\<user>\\OneDrive\\x.exe")]
    [InlineData("c:/users/maria/file", "c:/users/<user>/file")]
    public void Redact_esconde_o_nome_da_conta_do_windows(string input, string expected)
    {
        LogRedactor.Redact(input).ShouldBe(expected);
    }

    [Fact]
    public void Redact_nao_mexe_em_texto_comum_nem_em_versao_de_tres_numeros()
    {
        const string text = "apply module 'display': Changed, version 10.0.26100, 2560x1440";

        LogRedactor.Redact(text).ShouldBe(text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_devolve_vazio_para_entrada_vazia(string? input)
    {
        LogRedactor.Redact(input).ShouldBeEmpty();
    }
}
