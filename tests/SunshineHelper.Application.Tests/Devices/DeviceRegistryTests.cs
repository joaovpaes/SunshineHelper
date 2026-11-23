using Shouldly;
using SunshineHelper.Application.Devices;
using SunshineHelper.Application.Tests.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Devices;

public class DeviceRegistryTests
{
    private const string FilePath = "devices.json";

    private readonly FakeSettingsFileStore _store = new();

    private DeviceRegistry Create() => new(_store, FilePath);

    [Fact]
    public async Task Sem_arquivo_comeca_vazio()
    {
        using var registry = Create();

        await registry.LoadAsync(TestContext.Current.CancellationToken);

        registry.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Registrar_grava_no_arquivo_e_outra_instancia_le()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using (var first = Create())
        {
            await first.RecordConnectionAsync("Sala TV", "Perfil TV", cancellationToken);
            await first.RecordConnectionAsync("Sala TV", "Perfil TV", cancellationToken);
        }

        using var second = Create();
        await second.LoadAsync(cancellationToken);

        second.Devices.Count.ShouldBe(1);
        second.Devices[0].Name.ShouldBe("Sala TV");
        second.Devices[0].ConnectionCount.ShouldBe(2);
        second.Devices[0].LastProfileName.ShouldBe("Perfil TV");
    }

    [Fact]
    public async Task Nome_vazio_nao_grava_nada()
    {
        using var registry = Create();

        await registry.RecordConnectionAsync(null, "Perfil", TestContext.Current.CancellationToken);
        await registry.RecordConnectionAsync("  ", "Perfil", TestContext.Current.CancellationToken);

        registry.Devices.ShouldBeEmpty();
        _store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task O_arquivo_guarda_so_o_necessario()
    {
        using var registry = Create();

        await registry.RecordConnectionAsync("Sala TV", null, TestContext.Current.CancellationToken);

        string text = _store.Get(FilePath)!;
        text.ShouldContain("\"schemaVersion\": 1");
        text.ShouldContain("\"name\": \"Sala TV\"");
        text.ShouldContain("\"connectionCount\": 1");
    }

    [Fact]
    public async Task Esquecer_remove_e_grava()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var registry = Create();
        await registry.RecordConnectionAsync("Celular", null, cancellationToken);
        await registry.RecordConnectionAsync("TV", null, cancellationToken);

        await registry.ForgetAsync("Celular", cancellationToken);

        string.Join(",", registry.Devices.Select(d => d.Name)).ShouldBe("TV");
        using var reloaded = Create();
        await reloaded.LoadAsync(cancellationToken);
        string.Join(",", reloaded.Devices.Select(d => d.Name)).ShouldBe("TV");
    }

    [Fact]
    public async Task Arquivo_quebrado_comeca_vazio_e_o_proximo_registro_conserta()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store.Seed(FilePath, "{ isto nao e json");
        using var registry = Create();

        await registry.LoadAsync(cancellationToken);
        registry.Devices.ShouldBeEmpty();

        await registry.RecordConnectionAsync("TV", null, cancellationToken);

        using var reloaded = Create();
        await reloaded.LoadAsync(cancellationToken);
        reloaded.Devices.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Arquivo_em_branco_comeca_vazio()
    {
        _store.Seed(FilePath, "   ");
        using var registry = Create();

        await registry.LoadAsync(TestContext.Current.CancellationToken);

        registry.Devices.ShouldBeEmpty();
    }
}
