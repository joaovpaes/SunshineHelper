using Shouldly;
using SunshineHelper.Trigger.ElevatedClose;
using Xunit;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

public sealed class ElevatedCloseRequestTests
{
    private static readonly string[] _closeArgs = ["elevated-close", "close", "1234", "133000000000000000"];
    private static readonly string[] _killArgs = ["elevated-close", "kill", "8", "1"];
    private static readonly string[] _badNumberArgs = ["elevated-close", "kill", "abc", "1"];

    [Fact]
    public void Le_um_pedido_de_fechar()
    {
        ElevatedCloseRequest.TryParse(_closeArgs, out var request).ShouldBeTrue();

        request.ShouldBe(new ElevatedCloseRequest(ElevatedCloseOperation.Close, 1234, 133000000000000000));
    }

    [Fact]
    public void Le_um_pedido_de_encerrar()
    {
        ElevatedCloseRequest.TryParse(_killArgs, out var request).ShouldBeTrue();

        request.ShouldBe(new ElevatedCloseRequest(ElevatedCloseOperation.Kill, 8, 1));
    }

    [Fact]
    public void Recusa_sem_argumentos()
    {
        ElevatedCloseRequest.TryParse([], out var request).ShouldBeFalse();

        request.ShouldBeNull();
    }

    [Theory]
    [InlineData(new[] { "elevated-close" })]
    [InlineData(new[] { "elevated-close", "close", "1234" })]
    [InlineData(new[] { "elevated-close", "close", "1234", "5", "install" })]
    [InlineData(new[] { "start", "close", "1234", "5" })]
    [InlineData(new[] { "Elevated-Close", "close", "1234", "5" })]
    [InlineData(new[] { "elevated-close", "Close", "1234", "5" })]
    [InlineData(new[] { "elevated-close", "terminate", "1234", "5" })]
    [InlineData(new[] { "elevated-close", "close", "0", "5" })]
    [InlineData(new[] { "elevated-close", "close", "-12", "5" })]
    [InlineData(new[] { "elevated-close", "close", "+12", "5" })]
    [InlineData(new[] { "elevated-close", "close", " 12", "5" })]
    [InlineData(new[] { "elevated-close", "close", "12 ", "5" })]
    [InlineData(new[] { "elevated-close", "close", "1,234", "5" })]
    [InlineData(new[] { "elevated-close", "close", "0x10", "5" })]
    [InlineData(new[] { "elevated-close", "close", "99999999999", "5" })]
    [InlineData(new[] { "elevated-close", "close", "12", "0" })]
    [InlineData(new[] { "elevated-close", "close", "12", "-5" })]
    [InlineData(new[] { "elevated-close", "close", "12", "abc" })]
    [InlineData(new[] { "elevated-close", "close", "12", "" })]
    [InlineData(new[] { "elevated-close", "close", "12", "5\" install" })]
    public void Recusa_qualquer_outra_forma(string[] args)
    {
        ElevatedCloseRequest.TryParse(args, out var request).ShouldBeFalse();

        request.ShouldBeNull();
    }

    [Fact]
    public void Argumentos_ruins_saem_com_o_codigo_proprio_sem_tocar_em_processo()
    {
        ElevatedCloseMode.Run(_badNumberArgs).ShouldBe((int)ElevatedCloseExitCode.BadArguments);
    }
}
