using Shouldly;
using Xunit;
using SunshineHelper.Infrastructure.Monitoring;

namespace SunshineHelper.Infrastructure.Tests.Monitoring;

public sealed class SunshineLogSamplerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sampler").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Arquivo_inexistente_devolve_lista_vazia()
    {
        SunshineLogSampler.Read(Path.Combine(_dir, "nao-existe.log")).ShouldBeEmpty();
    }

    [Fact]
    public void Arquivo_pequeno_e_lido_inteiro()
    {
        string path = Path.Combine(_dir, "a.log");
        File.WriteAllText(path, "primeira\nsegunda\n");

        var lines = SunshineLogSampler.Read(path);

        lines.Count.ShouldBe(2);
        lines[1].ShouldBe("segunda");
    }

    [Fact]
    public void Arquivo_grande_traz_o_comeco_e_o_fim()
    {
        string path = Path.Combine(_dir, "b.log");
        string filler = new string('x', 99) + "\n";
        File.WriteAllText(path, "COMECO\n" + string.Concat(Enumerable.Repeat(filler, 5000)) + "FIM\n");

        var lines = SunshineLogSampler.Read(path);

        lines[0].ShouldBe("COMECO");
        lines[^1].ShouldBe("FIM");
    }

    [Fact]
    public void Arquivo_aberto_para_escrita_por_outro_processo_ainda_e_lido()
    {
        string path = Path.Combine(_dir, "c.log");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write("linha\n"u8);
        writer.Flush();

        SunshineLogSampler.Read(path).Count.ShouldBe(1);
    }
}
