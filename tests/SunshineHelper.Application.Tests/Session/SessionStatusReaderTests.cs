using Shouldly;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

public class SessionStatusReaderTests
{
    private static readonly DateTimeOffset _capturedAt = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sem_snapshot_o_estado_e_ocioso()
    {
        var reader = new SessionStatusReader(new FakeSessionSnapshotStore());

        var status = await reader.ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.Idle);
        status.ClientName.ShouldBeNull();
    }

    [Theory]
    [InlineData(SessionState.Applying)]
    [InlineData(SessionState.Applied)]
    [InlineData(SessionState.Reverting)]
    public async Task Snapshot_em_andamento_mostra_cliente_e_perfil(SessionState state)
    {
        var store = new FakeSessionSnapshotStore { Content = Serialized(state) };
        var reader = new SessionStatusReader(store);

        var status = await reader.ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.Active);
        status.ClientName.ShouldBe("Sala");
        status.ProfileId.ShouldBe("tv-4k");
        status.Since.ShouldBe(_capturedAt);
    }

    [Fact]
    public async Task Reversao_que_falhou_pede_atencao()
    {
        var store = new FakeSessionSnapshotStore { Content = Serialized(SessionState.RevertFailed) };
        var reader = new SessionStatusReader(store);

        var status = await reader.ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.NeedsAttention);
    }

    [Fact]
    public async Task Snapshot_ilegivel_pede_atencao_em_vez_de_quebrar()
    {
        var store = new FakeSessionSnapshotStore { Content = "{ não é json" };
        var reader = new SessionStatusReader(store);

        var status = await reader.ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.NeedsAttention);
    }

    [Fact]
    public async Task Erro_de_leitura_do_arquivo_pede_atencao_em_vez_de_quebrar()
    {
        var store = new FakeSessionSnapshotStore { ThrowOnRead = new IOException("arquivo travado") };
        var reader = new SessionStatusReader(store);

        var status = await reader.ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.NeedsAttention);
    }

    [Fact]
    public async Task Modulo_desfeito_sozinho_aparece_como_nao_aplicado()
    {
        var test = new OrchestratorTest();
        test.Modules[2].ApplyFails = "Sem permissão.";
        test.Modules[0].ApplyFails = "Sem permissão.";
        await test.Orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        var status = await new SessionStatusReader(test.Store).ReadAsync(TestContext.Current.CancellationToken);

        status.Kind.ShouldBe(SessionStatusKind.Active);
        string.Join(",", status.NotAppliedModules).ShouldBe("a,c");
    }

    [Fact]
    public async Task Sessao_sem_falhas_nao_tem_modulos_nao_aplicados()
    {
        var store = new FakeSessionSnapshotStore { Content = Serialized(SessionState.Applied) };

        var status = await new SessionStatusReader(store).ReadAsync(TestContext.Current.CancellationToken);

        status.NotAppliedModules.ShouldBeEmpty();
    }

    private static string Serialized(SessionState state) =>
        SessionSnapshotSerializer.Serialize(
            SessionSnapshot
                .Create("sessao-1", BootIdentity.Unknown, _capturedAt, [], "tv-4k", "Sala")
                .WithState(state));
}
