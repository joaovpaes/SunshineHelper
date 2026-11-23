using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Tests.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Protocol;
using Xunit;

namespace SunshineHelper.Application.Tests.Channel;

public class TriggerChannelHandlerTests
{
    [Fact]
    public async Task SessionStart_com_perfil_correspondente_aplica_e_responde_applied()
    {
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        var response = await handler.HandleAsync(StartRequest(), TestContext.Current.CancellationToken);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.Applied);
        response.Profile.ShouldBe(profile.Name);
        response.ElapsedMs.ShouldNotBeNull();
    }

    [Fact]
    public async Task SessionStart_sem_perfil_e_sem_padrao_responde_erro_sem_chamar_orquestrador()
    {
        var test = new OrchestratorTest();
        using var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(StartRequest(), TestContext.Current.CancellationToken);

        response.Ok.ShouldBeFalse();
        response.State.ShouldBe(ChannelState.Failed);
        response.Error.ShouldNotBeNullOrEmpty();
        test.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task SessionEnd_sem_sessao_registrada_responde_nothing_to_do()
    {
        var test = new OrchestratorTest();
        using var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(EndRequest(), TestContext.Current.CancellationToken);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.NothingToDo);
    }

    [Fact]
    public async Task SessionEnd_depois_de_aplicar_responde_reverted()
    {
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        await handler.HandleAsync(StartRequest(), TestContext.Current.CancellationToken);
        var response = await handler.HandleAsync(EndRequest(), TestContext.Current.CancellationToken);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.Reverted);
    }

    [Fact]
    public async Task HandleAsync_versao_de_protocolo_nao_suportada_responde_erro()
    {
        var test = new OrchestratorTest();
        using var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(
            new TriggerRequest { V = 99, Op = TriggerOp.SessionStart },
            TestContext.Current.CancellationToken);

        response.Ok.ShouldBeFalse();
        response.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_operacao_desconhecida_responde_erro()
    {
        var test = new OrchestratorTest();
        using var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(
            new TriggerRequest { Op = "nunca-ouvi-falar" },
            TestContext.Current.CancellationToken);

        response.Ok.ShouldBeFalse();
        response.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SessionStart_estourando_o_waitMs_responde_timeout_mas_o_trabalho_real_continua()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gated = new GatedApplyModule();
        var orchestrator = new SessionOrchestrator(
            [gated],
            new FakeSessionSnapshotStore(),
            new FakeBootIdentityProvider());
        var profile = TestProfiles.FollowClient();
        using var handler = new TriggerChannelHandler(
            orchestrator,
            new FakeCurrentSettingsProvider(Settings(defaultProfileId: profile.Id, profile)));

        // The apply is stuck inside the module, so the barrier is what answers.
        var timeoutResponse = await handler.HandleAsync(StartRequest(waitMs: 10), cancellationToken);

        timeoutResponse.Ok.ShouldBeTrue();
        timeoutResponse.State.ShouldBe(ChannelState.Timeout);

        // Let the real apply finish. session-end then waits its turn on the same gate,
        // so it can only run once that apply is genuinely done — which is the behavior
        // the ADR asks for: the reply races the client's patience, never the work. If
        // the "timeout" reply had abandoned the apply, there would be no session left
        // to revert here and this would come back as nothing-to-do.
        gated.Release();

        var endResponse = await handler.HandleAsync(EndRequest(), cancellationToken);

        endResponse.Ok.ShouldBeTrue();
        endResponse.State.ShouldBe(ChannelState.Reverted);
    }

    private static TriggerChannelHandler NewHandler(OrchestratorTest test, AppSettings settings) =>
        new(test.Orchestrator, new FakeCurrentSettingsProvider(settings));

    private static TriggerRequest StartRequest(int? waitMs = null) => new()
    {
        Op = TriggerOp.SessionStart,
        Client = new ClientInfo { Name = "Sala", Width = 3840, Height = 2160, Fps = 60, Hdr = true },
        WaitMs = waitMs,
    };

    private static TriggerRequest EndRequest() => new() { Op = TriggerOp.SessionEnd };

    private static AppSettings Settings(string? defaultProfileId, params ClientProfile[] profiles) => new()
    {
        SchemaVersion = SettingsSchema.CurrentVersion,
        DefaultProfileId = defaultProfileId,
        Profiles = profiles,
    };
}
