using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Tests.Launcher;
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

    [Fact]
    public async Task SessionEnd_por_desconexao_restaura_a_tela_mas_deixa_o_launcher_aberto()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processes = new FakeProcessController();
        var profile = TestProfiles.FollowClient();
        var settings = Settings(defaultProfileId: profile.Id, profile) with
        {
            Launcher = new LauncherSettings { ProcessNames = ["Playnite.FullscreenApp"] },
        };
        var settingsProvider = new FakeCurrentSettingsProvider(settings);
        var display = new FakeEnvironmentModule("display", 1);
        var launcher = new LauncherModule(processes, settingsProvider);
        var orchestrator = new SessionOrchestrator([display, launcher], new FakeSessionSnapshotStore(), new FakeBootIdentityProvider());
        using var handler = new TriggerChannelHandler(orchestrator, settingsProvider);

        await handler.HandleAsync(StartRequest(), cancellationToken);
        var playnite = processes.Start(77, "Playnite.FullscreenApp", DateTimeOffset.UtcNow);

        var response = await handler.HandleAsync(
            new TriggerRequest { Op = TriggerOp.SessionEnd, Reason = SessionEndReason.Disconnect },
            cancellationToken);

        response.State.ShouldBe(ChannelState.Reverted);
        display.Value.ShouldBe(FakeEnvironmentModule.Original);
        processes.Running.ShouldContain(playnite);
    }

    [Fact]
    public async Task SessionEnd_sem_motivo_e_cancel_e_fecha_o_launcher_aberto_na_sessao()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processes = new FakeProcessController();
        var profile = TestProfiles.FollowClient();
        var settings = Settings(defaultProfileId: profile.Id, profile) with
        {
            Launcher = new LauncherSettings { ProcessNames = ["Playnite.FullscreenApp"] },
        };
        var settingsProvider = new FakeCurrentSettingsProvider(settings);
        var launcher = new LauncherModule(processes, settingsProvider);
        var orchestrator = new SessionOrchestrator([launcher], new FakeSessionSnapshotStore(), new FakeBootIdentityProvider());
        using var handler = new TriggerChannelHandler(orchestrator, settingsProvider);

        await handler.HandleAsync(StartRequest(), cancellationToken);
        processes.Start(78, "Playnite.FullscreenApp", DateTimeOffset.UtcNow);
        processes.ClosesPolitely.Add(78);

        var response = await handler.HandleAsync(EndRequest(), cancellationToken);

        response.State.ShouldBe(ChannelState.Reverted);
        processes.Running.ShouldBeEmpty();
    }

    [Fact]
    public async Task SessionStart_com_modulo_que_falha_responde_applied_com_o_aviso()
    {
        var test = new OrchestratorTest();
        test.Modules[2].ApplyFails = "RTSS sem permissão.";
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, Settings(defaultProfileId: profile.Id, profile));

        var response = await handler.HandleAsync(StartRequest(), TestContext.Current.CancellationToken);

        response.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.Applied);
        response.Error.ShouldNotBeNull().ShouldContain("RTSS sem permissão.");
    }

    [Fact]
    public async Task O_log_mostra_o_cliente_e_o_tempo_de_uma_sessao_iniciada()
    {
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        var log = new ListDiagnosticLog();
        using var handler = new TriggerChannelHandler(test.Orchestrator, new FakeCurrentSettingsProvider(Settings(profile.Id, profile)), log);

        await handler.HandleAsync(StartRequest(), TestContext.Current.CancellationToken);

        log.Contains(DiagnosticLevel.Info, "client 'Sala' 3840x2160 hdr True").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "elapsed ").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, " ms").ShouldBeTrue();
    }

    [Fact]
    public async Task O_log_mostra_traco_quando_nao_ha_cliente_nem_tempo()
    {
        var test = new OrchestratorTest();
        var log = new ListDiagnosticLog();
        using var handler = new TriggerChannelHandler(test.Orchestrator, new FakeCurrentSettingsProvider(Settings(defaultProfileId: null)), log);

        await handler.HandleAsync(EndRequest(), TestContext.Current.CancellationToken);

        log.Contains(DiagnosticLevel.Info, "client -").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "elapsed -").ShouldBeTrue();
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
