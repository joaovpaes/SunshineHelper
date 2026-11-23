using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Monitoring;
using SunshineHelper.Application.Tests.Channel;
using SunshineHelper.Application.Tests.Session;
using SunshineHelper.Domain.Monitoring;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Protocol;
using Xunit;

namespace SunshineHelper.Application.Tests.Monitoring;

public class SunshineSessionMonitorTests
{
    [Fact]
    public async Task Desconexao_simples_mantem_o_ambiente_por_padrao()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient(); // RevertOnDisconnect defaults to false
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);
        test.Store.Exists().ShouldBeTrue();

        await monitor.HandleAsync(SunshineSessionEvent.ClientDisconnected, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.SessionEnded, cancellationToken);

        test.Store.Exists().ShouldBeTrue("uma desconexão simples não deveria reverter nada por padrão");
    }

    [Fact]
    public async Task Desconexao_com_RevertOnDisconnect_reverte_e_o_resume_reaplica()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient() with { RevertOnDisconnect = true };
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);
        test.Store.Exists().ShouldBeTrue();

        await monitor.HandleAsync(SunshineSessionEvent.ClientDisconnected, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.SessionEnded, cancellationToken);

        test.Store.Exists().ShouldBeFalse("o perfil pediu para reverter já na desconexão");

        await monitor.HandleAsync(SunshineSessionEvent.Resumed, cancellationToken);

        test.Store.Exists().ShouldBeTrue("o /resume deveria reaplicar o perfil que foi revertido");
    }

    [Fact]
    public async Task Resume_sem_nada_pendente_nao_faz_nada()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await monitor.HandleAsync(SunshineSessionEvent.Resumed, cancellationToken);

        test.Store.Exists().ShouldBeFalse();
        test.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resume_com_sessao_ainda_aplicada_nao_reaplica_de_novo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);
        int applyCountBefore = test.StepsOf("apply").Split(',').Length;

        // No disconnect happened, so there is nothing to resume from — the environment
        // is already exactly what the profile wants.
        await monitor.HandleAsync(SunshineSessionEvent.Resumed, cancellationToken);

        test.Store.Exists().ShouldBeTrue();
        test.StepsOf("apply").Split(',').Length.ShouldBe(applyCountBefore);
    }

    [Fact]
    public async Task ClientConnected_limpa_o_sinal_pendente_antes_do_session_ended()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient() with { RevertOnDisconnect = true };
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);

        // A disconnect immediately followed by a fresh connection, with "Session ended"
        // only arriving for the connection that is already back up — the monitor should
        // not treat this as the disconnect it saw a moment ago.
        await monitor.HandleAsync(SunshineSessionEvent.ClientDisconnected, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.ClientConnected, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.SessionEnded, cancellationToken);

        test.Store.Exists().ShouldBeTrue();
    }

    [Fact]
    public async Task Cancel_aciona_a_reversao_quando_o_pipe_ainda_nao_tinha_feito()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);
        test.Store.Exists().ShouldBeTrue();

        await monitor.HandleAsync(SunshineSessionEvent.CancelRequested, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.SessionEnded, cancellationToken);

        test.Store.Exists().ShouldBeFalse("o /cancel é sempre uma reversão, com ou sem o Undo Command do pipe");
    }

    [Fact]
    public async Task Cancel_nao_falha_quando_o_pipe_ja_tinha_revertido()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var monitor = NewMonitor(test, handler, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);

        // Simulates the Undo Command already having gone through the pipe before this
        // fallback ever sees the log line.
        await handler.HandleAsync(new TriggerRequest { Op = TriggerOp.SessionEnd }, cancellationToken);
        test.Store.Exists().ShouldBeFalse();

        await monitor.HandleAsync(SunshineSessionEvent.CancelRequested, cancellationToken);
        await monitor.HandleAsync(SunshineSessionEvent.SessionEnded, cancellationToken);

        test.Store.Exists().ShouldBeFalse("nada para reverter de novo; não deveria lançar nem recriar o snapshot");
    }

    [Fact]
    public async Task RunAsync_le_o_log_e_aciona_a_reversao_no_cancel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var profile = TestProfiles.FollowClient();
        using var handler = NewHandler(test, profile, out var settingsProvider);
        var source = new FakeSunshineLogSource(
        [
            "[2026-11-22 09:01:59.552]: Info: CLIENT CONNECTED",
            "[2026-11-22 09:02:33.736]: Debug: DESTINATION :: /cancel",
            "[2026-11-22 09:02:33.825]: Debug: Session ended",
        ]);
        var monitor = new SunshineSessionMonitor(source, handler, test.Store, settingsProvider);

        await test.Orchestrator.ApplyAsync(profile, TestProfiles.Request(), cancellationToken);

        await monitor.RunAsync(cancellationToken);

        test.Store.Exists().ShouldBeFalse();
    }

    private static TriggerChannelHandler NewHandler(
        OrchestratorTest test, ClientProfile profile, out FakeCurrentSettingsProvider settingsProvider)
    {
        settingsProvider = new FakeCurrentSettingsProvider(Settings(profile));
        return new TriggerChannelHandler(test.Orchestrator, settingsProvider);
    }

    private static SunshineSessionMonitor NewMonitor(
        OrchestratorTest test, TriggerChannelHandler handler, FakeCurrentSettingsProvider settingsProvider) =>
        new(new FakeSunshineLogSource([]), handler, test.Store, settingsProvider);

    private static AppSettings Settings(ClientProfile profile) => new()
    {
        SchemaVersion = SettingsSchema.CurrentVersion,
        DefaultProfileId = profile.Id,
        Profiles = [profile],
    };
}
