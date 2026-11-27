using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Application.Session;
using SunshineHelper.Application.Tests.Channel;
using SunshineHelper.Application.Tests.Session;
using SunshineHelper.Application.Tests.Settings;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.CloseGame;

public class CloseGameServiceTests
{
    private static readonly DateTimeOffset _sessionStart = new(2026, 11, 27, 20, 0, 0, TimeSpan.Zero);
    private static readonly string[] _noNames = [];
    private static readonly string[] _gameProtected = ["Hades2.exe"];

    private readonly FakeGameEnvironment _env = new();
    private readonly FakeSessionSnapshotStore _store = new();
    private readonly FakeTimeProvider _time = new(_sessionStart.AddMinutes(30));
    private readonly ListDiagnosticLog _log = new();
    private readonly FakeCurrentSettingsProvider _settings = new(Settings());
    private readonly CloseGameService _service;

    private readonly ProcessIdentity _playnite;
    private readonly ProcessIdentity _game;

    public CloseGameServiceTests()
    {
        _store.Content = SessionSnapshotSerializer.Serialize(
            SessionSnapshot.Create("sessao-1", BootIdentity.Unknown, _sessionStart, [], "tv-4k", "Sala").WithState(SessionState.Applied));
        _service = new CloseGameService(_env, _env, _settings, new SessionStatusReader(_store), _time, _log);

        _playnite = _env.Start(100, "Playnite.FullscreenApp", _sessionStart.AddSeconds(2));
        _game = _env.Start(200, "Hades2", _sessionStart.AddMinutes(1));
        _env.Focus(_game);
    }

    private static AppSettings Settings(bool enabled = true, bool force = false, bool returnToLauncher = true, string[]? launcher = null, string[]? protectedNames = null) =>
        AppSettings.CreateDefault() with
        {
            Launcher = new LauncherSettings { ProcessNames = launcher ?? ["Playnite.FullscreenApp"] },
            Controller = new ControllerSettings
            {
                CloseGame = new CloseGameSettings
                {
                    Enabled = enabled,
                    ForceWithoutConfirm = force,
                    ReturnToLauncher = returnToLauncher,
                    PoliteTimeoutMs = 5000,
                    ProtectedNames = protectedNames ?? [],
                },
            },
        };

    private Task<CloseGameOutcome> HoldAsync() => _service.HandleComboAsync(TestContext.Current.CancellationToken);

    private void Advance(TimeSpan by) => _time.Now += by;

    [Fact]
    public async Task Desligado_nao_olha_nada()
    {
        _settings.Current = Settings(enabled: false);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Disabled);

        _env.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_transmissao_nao_fecha()
    {
        _store.Content = null;

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.NoSession);

        _env.Calls.ShouldNotContain("foreground");
    }

    [Fact]
    public async Task Sessao_que_falhou_ao_restaurar_nao_conta_como_transmissao()
    {
        _store.Content = SessionSnapshotSerializer.Serialize(
            SessionSnapshot.Create("sessao-1", BootIdentity.Unknown, _sessionStart, []).WithState(SessionState.RevertFailed));

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.NoSession);
    }

    [Fact]
    public async Task Sem_janela_em_foco_nao_faz_nada()
    {
        _env.Foreground = ForegroundWindow.Nothing;

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.NoTarget);
    }

    [Fact]
    public async Task Janela_que_nao_pode_ser_inspecionada_avisa_do_administrador()
    {
        _env.Foreground = new ForegroundWindow(ForegroundStatus.NotInspectable);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.NotInspectable);

        _log.Contains(DiagnosticLevel.Warning, "administrator").ShouldBeTrue();
    }

    [Fact]
    public async Task Jogo_que_fecha_ao_pedir_fecha_e_volta_ao_launcher()
    {
        _env.ClosesPolitely.Add(_game.Id);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        outcome.ProcessName.ShouldBe("Hades2");
        _env.Calls.ShouldContain("closeWindow:200:2000");
        _env.Calls.ShouldContain("wait:200:5000");
        _env.Calls.ShouldContain("front:Playnite.FullscreenApp");
        _env.Calls.ShouldNotContain(c => c.StartsWith("kill", StringComparison.Ordinal));
        _env.Running.ShouldContain(_playnite);
    }

    [Fact]
    public async Task O_launcher_em_foco_nunca_e_fechado()
    {
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.Launcher);
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
        _env.Running.ShouldContain(_playnite);
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("sunshine")]
    [InlineData("SunshineHelper")]
    [InlineData("ApplicationFrameHost")]
    public async Task Programas_do_windows_do_sunshine_e_do_app_sao_protegidos(string name)
    {
        _env.Focus(_env.Start(300, name, _sessionStart.AddMinutes(5)));

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.Fixed);
    }

    [Fact]
    public async Task Lista_de_protegidos_do_usuario_e_respeitada()
    {
        _settings.Current = Settings(protectedNames: _gameProtected);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.UserList);
    }

    [Fact]
    public async Task Programa_aberto_antes_da_transmissao_e_protegido()
    {
        _env.Focus(_env.Start(400, "Discord", _sessionStart.AddHours(-2)));

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.StartedBeforeSession);
    }

    [Fact]
    public async Task Pausa_depois_de_fechar_ignora_o_combo_mesmo_com_o_playnite_em_foco()
    {
        _env.ClosesPolitely.Add(_game.Id);
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);
        _env.Focus(_playnite);
        _env.Calls.Clear();

        Advance(TimeSpan.FromSeconds(4));
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.CoolingDown);
        _env.Calls.ShouldBeEmpty();

        Advance(TimeSpan.FromSeconds(2));
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Protected);
        _env.Running.ShouldContain(_playnite);
    }

    [Fact]
    public async Task Jogo_sem_menu_sair_pede_segunda_segurada_e_encerra_o_mesmo_processo()
    {
        var first = await HoldAsync();
        first.Result.ShouldBe(CloseGameResult.AwaitingConfirmation);
        _service.PendingConfirmation.ShouldBe(_game);
        _env.Calls.ShouldNotContain(c => c.StartsWith("kill", StringComparison.Ordinal));

        // O foco mudou para o Playnite: a segunda segurada continua mirando o jogo.
        _env.Focus(_playnite);
        Advance(TimeSpan.FromSeconds(8));
        var second = await HoldAsync();

        second.Result.ShouldBe(CloseGameResult.Forced);
        second.ProcessName.ShouldBe("Hades2");
        _env.Calls.ShouldContain("killTree:200");
        _env.Running.ShouldNotContain(_game);
        _env.Running.ShouldContain(_playnite);
        _service.PendingConfirmation.ShouldBeNull();
    }

    [Fact]
    public async Task Confirmacao_expira_e_a_segurada_seguinte_comeca_do_zero()
    {
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.AwaitingConfirmation);
        _env.Focus(_playnite);

        Advance(TimeSpan.FromSeconds(16));
        _service.PendingConfirmation.ShouldBeNull();
        var later = await HoldAsync();

        later.Result.ShouldBe(CloseGameResult.Protected);
        _env.Running.ShouldContain(_game);
        _env.Calls.ShouldNotContain(c => c.StartsWith("kill", StringComparison.Ordinal));
        _log.Contains(DiagnosticLevel.Info, "expired").ShouldBeTrue();
    }

    [Fact]
    public async Task Encerrar_sem_confirmar_encerra_na_primeira_segurada()
    {
        _settings.Current = Settings(force: true);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Forced);
        _env.Calls.ShouldContain("killTree:200");
    }

    [Fact]
    public async Task Jogo_que_nao_morre_vira_aviso_e_pausa()
    {
        _env.Unkillable.Add(_game.Id);
        await HoldAsync();

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.ForceFailed);
        _log.Contains(DiagnosticLevel.Warning, "could not end 'Hades2'").ShouldBeTrue();
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.CoolingDown);
    }

    [Fact]
    public async Task Filho_protegido_faz_encerrar_so_o_jogo()
    {
        _env.Children[_game.Id] = [new ProcessIdentity(500, "explorer", _sessionStart.AddMinutes(2))];
        _settings.Current = Settings(force: true);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Forced);
        _env.Calls.ShouldContain("killOne:200");
        _env.Calls.ShouldNotContain("killTree:200");
    }

    [Fact]
    public async Task Pedido_recusado_pelo_windows_avisa_do_administrador()
    {
        _env.DeniesClose.Add(_game.Id);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _log.Contains(DiagnosticLevel.Warning, "administrator").ShouldBeTrue();
        _env.Calls.ShouldNotContain(c => c.StartsWith("wait", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Jogo_que_ja_tinha_fechado_conta_como_fechado()
    {
        _env.Running.Remove(_game);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);
    }

    [Fact]
    public async Task Segurada_durante_a_espera_e_ignorada()
    {
        _env.WaitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _env.ClosesPolitely.Add(_game.Id);

        var first = HoldAsync();
        var second = await HoldAsync();
        _env.WaitGate.SetResult();

        second.Result.ShouldBe(CloseGameResult.Busy);
        (await first).Result.ShouldBe(CloseGameResult.Closed);
    }

    [Fact]
    public async Task Voltar_ao_launcher_desligado_nao_mexe_no_foco()
    {
        _settings.Current = Settings(returnToLauncher: false);
        _env.ClosesPolitely.Add(_game.Id);

        await HoldAsync();

        _env.Calls.ShouldNotContain(c => c.StartsWith("front", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sem_launcher_configurado_nao_tenta_voltar()
    {
        _settings.Current = Settings(launcher: _noNames);
        _env.ClosesPolitely.Add(_game.Id);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);

        _env.Calls.ShouldNotContain(c => c.StartsWith("front", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Windows_recusar_trazer_o_launcher_so_fica_no_log()
    {
        _env.BringToFrontWorks = false;
        _env.ClosesPolitely.Add(_game.Id);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);

        _log.Contains(DiagnosticLevel.Info, "did not let the app bring").ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_esquece_a_confirmacao_e_a_pausa()
    {
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.AwaitingConfirmation);

        _service.Reset();

        _service.PendingConfirmation.ShouldBeNull();
        _env.Focus(_playnite);
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Protected);
        _env.Running.ShouldContain(_game);
    }

    [Fact]
    public void Construtor_recusa_nulos()
    {
        var reader = new SessionStatusReader(_store);

        Should.Throw<ArgumentNullException>(() => new CloseGameService(null!, _env, _settings, reader));
        Should.Throw<ArgumentNullException>(() => new CloseGameService(_env, null!, _settings, reader));
        Should.Throw<ArgumentNullException>(() => new CloseGameService(_env, _env, null!, reader));
        Should.Throw<ArgumentNullException>(() => new CloseGameService(_env, _env, _settings, null!));
    }
}
