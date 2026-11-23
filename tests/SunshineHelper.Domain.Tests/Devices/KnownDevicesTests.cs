using Shouldly;
using SunshineHelper.Domain.Devices;
using Xunit;

namespace SunshineHelper.Domain.Tests.Devices;

public class KnownDevicesTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 23, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Primeira_conexao_cria_o_aparelho()
    {
        var list = KnownDevices.Record([], "Sala TV", "Perfil TV", _t0);

        list.Count.ShouldBe(1);
        list[0].Name.ShouldBe("Sala TV");
        list[0].ConnectionCount.ShouldBe(1);
        list[0].FirstSeen.ShouldBe(_t0);
        list[0].LastSeen.ShouldBe(_t0);
        list[0].LastProfileName.ShouldBe("Perfil TV");
    }

    [Fact]
    public void Nova_conexao_soma_e_atualiza_a_ultima_vez_e_o_perfil()
    {
        var first = KnownDevices.Record([], "Sala TV", null, _t0);

        var list = KnownDevices.Record(first, "Sala TV", "Perfil TV", _t0.AddHours(1));

        list.Count.ShouldBe(1);
        list[0].ConnectionCount.ShouldBe(2);
        list[0].FirstSeen.ShouldBe(_t0);
        list[0].LastSeen.ShouldBe(_t0.AddHours(1));
        list[0].LastProfileName.ShouldBe("Perfil TV");
    }

    [Fact]
    public void O_nome_ignora_maiusculas_e_espacos_nas_pontas()
    {
        var first = KnownDevices.Record([], "Sala TV", null, _t0);

        var list = KnownDevices.Record(first, "  sala tv ", null, _t0.AddMinutes(5));

        list.Count.ShouldBe(1);
        list[0].Name.ShouldBe("Sala TV");
        list[0].ConnectionCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_e_ignorado(string? name)
    {
        var list = KnownDevices.Record([], name, "Perfil", _t0);

        list.ShouldBeEmpty();
    }

    [Fact]
    public void O_mais_recente_vem_primeiro()
    {
        var list = KnownDevices.Record([], "Celular", null, _t0);
        list = KnownDevices.Record(list, "TV", null, _t0.AddHours(1));
        list = KnownDevices.Record(list, "Celular", null, _t0.AddHours(2));

        string.Join(",", list.Select(d => d.Name)).ShouldBe("Celular,TV");
    }

    [Fact]
    public void Nome_muito_longo_e_cortado()
    {
        var list = KnownDevices.Record([], new string('a', KnownDevices.MaxNameLength + 50), null, _t0);

        list[0].Name.Length.ShouldBe(KnownDevices.MaxNameLength);
    }

    [Fact]
    public void A_lista_tem_um_limite_e_perde_os_mais_antigos()
    {
        IReadOnlyList<KnownDevice> list = [];
        for (int i = 0; i < KnownDevices.MaxDevices + 5; i++)
        {
            list = KnownDevices.Record(list, $"Aparelho {i}", null, _t0.AddMinutes(i));
        }

        list.Count.ShouldBe(KnownDevices.MaxDevices);
        list.Any(d => d.Name == "Aparelho 0").ShouldBeFalse();
        list[0].Name.ShouldBe($"Aparelho {KnownDevices.MaxDevices + 4}");
    }

    [Fact]
    public void Esquecer_remove_so_o_aparelho_pedido()
    {
        var list = KnownDevices.Record([], "Celular", null, _t0);
        list = KnownDevices.Record(list, "TV", null, _t0.AddHours(1));

        var after = KnownDevices.Forget(list, "celular");

        string.Join(",", after.Select(d => d.Name)).ShouldBe("TV");
    }

    [Fact]
    public void Esquecer_um_nome_que_nao_existe_nao_muda_nada()
    {
        var list = KnownDevices.Record([], "TV", null, _t0);

        KnownDevices.Forget(list, "outro").Count.ShouldBe(1);
    }

    [Fact]
    public void Normalizar_tira_repetidos_e_nomes_vazios()
    {
        KnownDevice[] raw =
        [
            new() { Name = "TV", FirstSeen = _t0, LastSeen = _t0, ConnectionCount = 1 },
            new() { Name = "tv", FirstSeen = _t0, LastSeen = _t0.AddHours(1), ConnectionCount = 4, LastProfileName = "P" },
            new() { Name = "  ", FirstSeen = _t0, LastSeen = _t0, ConnectionCount = 1 },
        ];

        var list = KnownDevices.Normalize(raw);

        list.Count.ShouldBe(1);
        list[0].LastSeen.ShouldBe(_t0.AddHours(1));
        list[0].ConnectionCount.ShouldBe(4);
    }
}
