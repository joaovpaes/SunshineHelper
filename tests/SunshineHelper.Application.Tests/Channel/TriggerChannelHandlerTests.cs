using Shouldly;
using SunshineHelper.Application.Channel;
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
        var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        var response = await handler.HandleAsync(StartRequest(), CancellationToken.None);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.Applied);
        response.Profile.ShouldBe(profile.Name);
        response.ElapsedMs.ShouldNotBeNull();
    }

    [Fact]
    public async Task SessionStart_sem_perfil_e_sem_padrao_responde_erro_sem_chamar_orquestrador()
    {
        var test = new OrchestratorTest();
        var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(StartRequest(), CancellationToken.None);

        response.Ok.ShouldBeFalse();
        response.State.ShouldBe(ChannelState.Failed);
        response.Error.ShouldNotBeNullOrEmpty();
        test.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task SessionEnd_sem_sessao_registrada_responde_nothing_to_do()
    {
        var test = new OrchestratorTest();
        var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(EndRequest(), CancellationToken.None);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.NothingToDo);
    }

    [Fact]
    public async Task SessionEnd_depois_de_aplicar_responde_reverted()
    {
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        await handler.HandleAsync(StartRequest(), CancellationToken.None);
        var response = await handler.HandleAsync(EndRequest(), CancellationToken.None);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.Reverted);
    }

    [Fact]
    public async Task HandleAsync_versao_de_protocolo_nao_suportada_responde_erro()
    {
        var test = new OrchestratorTest();
        var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(
            new TriggerRequest { V = 99, Op = TriggerOp.SessionStart },
            CancellationToken.None);

        response.Ok.ShouldBeFalse();
        response.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_operacao_desconhecida_responde_erro()
    {
        var test = new OrchestratorTest();
        var handler = NewHandler(test, Settings(defaultProfileId: null));

        var response = await handler.HandleAsync(
            new TriggerRequest { Op = "nunca-ouvi-falar" },
            CancellationToken.None);

        response.Ok.ShouldBeFalse();
        response.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SessionStart_estourando_o_waitMs_responde_timeout_mas_o_trabalho_real_continua()
    {
        var test = new OrchestratorTest();
        test.Modules[0].WhileApplying = () => Thread.Sleep(150);
        var profile = TestProfiles.FollowClient();
        var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        var timeoutResponse = await handler.HandleAsync(StartRequest(waitMs: 10), CancellationToken.None);

        timeoutResponse.Ok.ShouldBeTrue();
        timeoutResponse.State.ShouldBe(ChannelState.Timeout);

        // session-end waits its turn on the same gate as the still-running apply, so by
        // the time it runs the apply that was "timed out" on has already finished for
        // real — exactly the behavior the ADR calls for: the reply races the client's
        // patience, never the actual work.
        var endResponse = await handler.HandleAsync(EndRequest(), CancellationToken.None);

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
