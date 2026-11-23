using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Monitoring;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Tests.Channel;
using SunshineHelper.Application.Tests.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Protocol;
using Xunit;

namespace SunshineHelper.Application.Tests.Monitoring;

public class SunshineProcessWatchdogTests
{
    private const int SunshinePid = 4242;

    [Fact]
    public async Task Sem_sessao_nao_consulta_os_processos()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe();
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse();

        probe.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Sunshine_rodando_mantem_a_sessao()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);
        await StartSessionAsync(test);

        for (int i = 0; i < 5; i++)
        {
            (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse();
        }

        test.Store.Exists().ShouldBeTrue();
        test.Environment.ShouldBe("applied,applied,applied");
    }

    [Fact]
    public async Task Sunshine_encerrado_restaura_depois_de_duas_verificacoes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        var log = new ListDiagnosticLog();
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store, TimeProvider.System, log);
        await StartSessionAsync(test);

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse(); // notes the pid
        probe.Running.Clear();

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse("uma leitura só não basta");
        test.Store.Exists().ShouldBeTrue();

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeTrue();
        test.Store.Exists().ShouldBeFalse();
        test.Environment.ShouldBe("original,original,original");
        log.Contains(DiagnosticLevel.Warning, "Sunshine process of this session is gone").ShouldBeTrue();
    }

    [Fact]
    public async Task Uma_falta_isolada_nao_restaura()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);
        await StartSessionAsync(test);

        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();
        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Add(SunshinePid);
        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse("a contagem recomeça quando o processo aparece de novo");
        test.Store.Exists().ShouldBeTrue();
    }

    [Fact]
    public async Task Sunshine_reiniciado_com_outro_pid_restaura()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);
        await StartSessionAsync(test);

        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();
        probe.Running.Add(SunshinePid + 1);

        await watchdog.CheckAsync(cancellationToken);
        (await watchdog.CheckAsync(cancellationToken)).ShouldBeTrue();
        test.Store.Exists().ShouldBeFalse();
    }

    [Fact]
    public async Task Sem_processo_do_Sunshine_nao_restaura_e_avisa_uma_vez()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe();
        var log = new ListDiagnosticLog();
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store, TimeProvider.System, log);
        await StartSessionAsync(test);

        for (int i = 0; i < 5; i++)
        {
            (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse();
        }

        test.Store.Exists().ShouldBeTrue();
        log.Entries.Count(entry => entry.Message.Contains("no Sunshine process found", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Restauracao_que_falha_nao_e_repetida()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);
        await StartSessionAsync(test);
        test.Modules[0].RevertThrows = new InvalidOperationException("falhou de propósito");

        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();
        await watchdog.CheckAsync(cancellationToken);
        (await watchdog.CheckAsync(cancellationToken)).ShouldBeTrue();
        int reverts = test.StepsOf("revert").Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

        for (int i = 0; i < 4; i++)
        {
            (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse();
        }

        test.Store.Exists().ShouldBeTrue("a restauração falhou, então o snapshot fica");
        test.StepsOf("revert").Split(',', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(reverts);
    }

    [Fact]
    public async Task Sessao_nova_volta_a_ser_vigiada()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Running = { SunshinePid } };
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);

        await StartSessionAsync(test);
        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();
        await watchdog.CheckAsync(cancellationToken);
        (await watchdog.CheckAsync(cancellationToken)).ShouldBeTrue();
        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse(); // no session: forgets

        probe.Running.Add(SunshinePid + 7);
        await StartSessionAsync(test);
        await watchdog.CheckAsync(cancellationToken);
        probe.Running.Clear();
        await watchdog.CheckAsync(cancellationToken);

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeTrue();
        test.Store.Exists().ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_ilegivel_nao_faz_nada()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe();
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store);
        test.Store.Content = "{ isto não é um snapshot";

        (await watchdog.CheckAsync(cancellationToken)).ShouldBeFalse();

        probe.Calls.ShouldBe(0);
        test.Store.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_segue_depois_de_uma_verificacao_com_erro_e_para_ao_cancelar()
    {
        var test = new OrchestratorTest();
        var probe = new FakeSunshineProcessProbe { Throws = new InvalidOperationException("sem lista") };
        var log = new ListDiagnosticLog();
        using var handler = NewHandler(test);
        var watchdog = new SunshineProcessWatchdog(probe, handler, test.Store, TimeProvider.System, log);
        await StartSessionAsync(test);

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = watchdog.RunAsync(TimeSpan.FromMilliseconds(1), stopping.Token);
        while (probe.Calls < 2)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        await stopping.CancelAsync();
        await run;

        log.Contains(DiagnosticLevel.Error, "check failed").ShouldBeTrue();
    }

    [Fact]
    public async Task Fim_condicional_ignora_outra_sessao()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        using var handler = NewHandler(test);
        await StartSessionAsync(test);

        var response = await handler.EndSessionIfCurrentAsync("outra-sessao", SessionEndReason.SunshineExited, cancellationToken);

        response.State.ShouldBe(ChannelState.NothingToDo);
        test.Store.Exists().ShouldBeTrue();
    }

    [Fact]
    public async Task Fim_condicional_restaura_a_sessao_atual()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();
        using var handler = NewHandler(test);
        await StartSessionAsync(test);
        string? sessionId = await test.Orchestrator.ReadSessionIdAsync(cancellationToken);
        sessionId.ShouldNotBeNull();

        var response = await handler.EndSessionIfCurrentAsync(sessionId, SessionEndReason.SunshineExited, cancellationToken);

        response.State.ShouldBe(ChannelState.Reverted);
        test.Store.Exists().ShouldBeFalse();
    }

    [Fact]
    public async Task Id_da_sessao_e_nulo_sem_snapshot_ou_com_snapshot_ilegivel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var test = new OrchestratorTest();

        (await test.Orchestrator.ReadSessionIdAsync(cancellationToken)).ShouldBeNull();

        test.Store.Content = "não é json";
        (await test.Orchestrator.ReadSessionIdAsync(cancellationToken)).ShouldBeNull();
    }

    private static async Task StartSessionAsync(OrchestratorTest test)
    {
        var result = await test.Orchestrator.ApplyAsync(
            TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(SessionApplyStatus.Applied);
    }

    private static TriggerChannelHandler NewHandler(OrchestratorTest test)
    {
        var profile = TestProfiles.FollowClient();
        var settings = new AppSettings
        {
            SchemaVersion = SettingsSchema.CurrentVersion,
            DefaultProfileId = profile.Id,
            Profiles = [profile],
        };
        return new TriggerChannelHandler(test.Orchestrator, new FakeCurrentSettingsProvider(settings));
    }
}
