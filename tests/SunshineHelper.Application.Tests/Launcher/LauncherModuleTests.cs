using System.Threading;
using Shouldly;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Tests.Channel;
using SunshineHelper.Application.Tests.Settings;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Launcher;

public class LauncherModuleTests
{
    private const string Playnite = "Playnite.FullscreenApp";

    private static readonly DateTimeOffset _sessionStart = new(2026, 11, 23, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeProcessController _processes = new();
    private readonly FakeTimeProvider _clock = new(_sessionStart);
    private readonly List<TimeSpan> _delays = [];

    [Fact]
    public async Task Sem_lista_de_processos_nao_procura_nem_fecha_nada()
    {
        var module = NewModule(Settings(names: []));
        _processes.Start(10, Playnite, _sessionStart.AddSeconds(5));

        var snapshot = await module.CaptureAsync(Token);
        var applied = await module.ApplyAsync(new DesiredEnvironment(), Token);
        var reverted = await module.RevertAsync(snapshot, Token);

        applied.Status.ShouldBe(ApplyStatus.NothingToDo);
        reverted.Reverted.ShouldBeTrue();
        _processes.Calls.ShouldBeEmpty();
        _processes.Running.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Launcher_aberto_durante_a_sessao_e_fechado_com_educacao()
    {
        var module = NewModule(Settings(names: [Playnite]));
        var snapshot = await module.CaptureAsync(Token);
        (await module.ApplyAsync(new DesiredEnvironment(), Token)).Status.ShouldBe(ApplyStatus.Changed);

        _processes.Start(20, Playnite, _sessionStart.AddSeconds(3));
        _processes.ClosesPolitely.Add(20);

        var reverted = await module.RevertAsync(snapshot, Token);

        reverted.ShouldBe(RevertOutcome.Success());
        _processes.Running.ShouldBeEmpty();
        _processes.Calls.ShouldNotContain("kill:20");
    }

    [Fact]
    public async Task Launcher_que_ignora_o_pedido_e_encerrado_com_a_arvore_depois_do_teto()
    {
        var module = NewModule(Settings(names: [Playnite], closeTimeoutMs: 7_000));
        var snapshot = await module.CaptureAsync(Token);
        _processes.Start(21, Playnite, _sessionStart.AddSeconds(3));

        var reverted = await module.RevertAsync(snapshot, Token);

        reverted.ShouldBe(RevertOutcome.Success());
        _processes.Calls.ShouldBe(new[] { "find:" + Playnite, "find:" + Playnite, "close:21", "wait:21:7000", "kill:21" });
        _processes.Running.ShouldBeEmpty();
    }

    [Fact]
    public async Task Launcher_que_ja_estava_aberto_antes_da_sessao_nunca_e_fechado()
    {
        var mine = _processes.Start(30, Playnite, _sessionStart.AddMinutes(-20));
        var module = NewModule(Settings(names: [Playnite]));

        var snapshot = await module.CaptureAsync(Token);
        var reverted = await module.RevertAsync(snapshot, Token);

        reverted.Reverted.ShouldBeTrue();
        _processes.Running.ShouldContain(mine);
        _processes.Calls.Any(call => call.StartsWith("close:", StringComparison.Ordinal) || call.StartsWith("kill:", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Pid_reaproveitado_por_outro_processo_iniciado_antes_da_sessao_nao_e_fechado()
    {
        var module = NewModule(Settings(names: [Playnite]));
        var snapshot = await module.CaptureAsync(Token);

        // Not in the baseline (it was not running at capture time as far as the list
        // showed), but it claims to have started well before the session did.
        _processes.Start(40, Playnite, _sessionStart.AddMinutes(-5));

        await module.RevertAsync(snapshot, Token);

        _processes.Running.Count.ShouldBe(1);
        _processes.Calls.ShouldNotContain("kill:40");
    }

    [Fact]
    public async Task Processo_que_nao_morre_vira_aviso_e_nao_falha()
    {
        var module = NewModule(Settings(names: [Playnite]));
        var snapshot = await module.CaptureAsync(Token);
        _processes.Start(50, Playnite, _sessionStart.AddSeconds(2));
        _processes.Unkillable.Add(50);

        var reverted = await module.RevertAsync(snapshot, Token);

        reverted.Reverted.ShouldBeTrue();
        reverted.ErrorMessage.ShouldNotBeNull();
        reverted.ErrorMessage.ShouldContain("PID 50");
    }

    [Fact]
    public async Task A_lista_usada_no_fim_e_a_do_inicio_da_sessao_nao_a_editada_depois()
    {
        var settings = new FakeCurrentSettingsProvider(Settings(names: [Playnite]));
        var module = NewModule(settings);
        var snapshot = await module.CaptureAsync(Token);

        settings.Current = Settings(names: ["OutroLauncher"]);
        _processes.Start(60, Playnite, _sessionStart.AddSeconds(1));
        _processes.Start(61, "OutroLauncher", _sessionStart.AddSeconds(1));

        await module.RevertAsync(snapshot, Token);

        _processes.Running.ShouldHaveSingleItem().Id.ShouldBe(61);
    }

    [Fact]
    public async Task Nomes_com_exe_e_nomes_proibidos_sao_tratados_antes_de_procurar()
    {
        var module = NewModule(Settings(names: ["Playnite.FullscreenApp.exe", "explorer.exe", "  "]));

        await module.CaptureAsync(Token);

        _processes.Calls.ShouldBe(new[] { "find:" + Playnite });
    }

    [Fact]
    public async Task Atraso_do_perfil_vence_o_global_e_zero_nao_espera()
    {
        var module = NewModule(Settings(names: [], globalDelayMs: 4_000));

        await module.ApplyAsync(new DesiredEnvironment(), Token);
        await module.ApplyAsync(new DesiredEnvironment { LauncherDelayMs = 1_500 }, Token);
        await module.ApplyAsync(new DesiredEnvironment { LauncherDelayMs = 0 }, Token);

        _delays.ShouldBe(new[] { TimeSpan.FromMilliseconds(4_000), TimeSpan.FromMilliseconds(1_500) });
    }

    [Fact]
    public async Task Snapshot_de_versao_desconhecida_e_recusado()
    {
        var module = NewModule(Settings(names: [Playnite]));

        var reverted = await module.RevertAsync(new ModuleSnapshot { PayloadVersion = 99 }, Token);

        reverted.Reverted.ShouldBeFalse();
    }

    [Fact]
    public void Nao_sobrevive_a_reinicio_e_reverte_antes_dos_outros_modulos()
    {
        var module = NewModule(Settings(names: []));

        module.Id.ShouldBe(LauncherModule.ModuleId);
        module.SurvivesReboot.ShouldBeFalse();
        module.Order.ShouldBeGreaterThan(3);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LauncherModule NewModule(AppSettings settings) => NewModule(new FakeCurrentSettingsProvider(settings));

    private LauncherModule NewModule(FakeCurrentSettingsProvider settings) =>
        new(_processes, settings, _clock, (delay, _) =>
        {
            _delays.Add(delay);
            return Task.CompletedTask;
        });

    private static AppSettings Settings(string[] names, int closeTimeoutMs = 10_000, int globalDelayMs = 0) =>
        AppSettings.CreateDefault() with
        {
            Timing = new TimingSettings { LauncherDelayMs = globalDelayMs },
            Launcher = new LauncherSettings { ProcessNames = names, CloseTimeoutMs = closeTimeoutMs },
        };
}
