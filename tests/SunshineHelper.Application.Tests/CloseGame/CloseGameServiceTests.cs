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
    private static readonly string[] _helperProtected = ["PlayniteHelper"];

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
        _service = new CloseGameService(_env, _env, _settings, new SessionStatusReader(_store), _time, _log, _env);

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
    public async Task Janela_que_nao_pode_ser_identificada_nao_fecha_nada()
    {
        _env.Foreground = new ForegroundWindow(ForegroundStatus.NotInspectable);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.NotInspectable);

        _log.Contains(DiagnosticLevel.Warning, "could not be identified").ShouldBeTrue();
        _env.Calls.ShouldNotContain(c => c.StartsWith("task", StringComparison.Ordinal));
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

    [Fact]
    public async Task Launcher_em_foco_fecha_o_jogo_que_ele_abriu()
    {
        // Playnite em tela cheia fica com o foco enquanto o emulador mostra o jogo.
        var emulator = _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(5));
        _env.ClosesPolitely.Add(emulator.Id);
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        outcome.ProcessName.ShouldBe("Ryujinx");
        _env.Calls.ShouldContain("closeWindow:300:3000");
        _env.Running.ShouldContain(_playnite);
        _env.Running.ShouldNotContain(emulator);
        _log.Contains(DiagnosticLevel.Info, "closing the game it started, 'Ryujinx'").ShouldBeTrue();
    }

    [Fact]
    public async Task Launcher_em_foco_com_dois_programas_com_janela_nao_adivinha()
    {
        _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(5));
        _env.StartWindowedChild(_playnite, 400, "OutroJogo", _sessionStart.AddMinutes(6));
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AmbiguousTarget);
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
        _log.Contains(DiagnosticLevel.Info, "Ryujinx, OutroJogo").ShouldBeTrue();
    }

    [Fact]
    public async Task Filho_do_launcher_aberto_antes_da_transmissao_nao_e_alvo()
    {
        _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(-10));
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.Launcher);
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Filho_protegido_do_launcher_e_ignorado_e_o_jogo_e_o_alvo()
    {
        _settings.Current = Settings(protectedNames: _helperProtected);
        _env.StartWindowedChild(_playnite, 300, "PlayniteHelper", _sessionStart.AddMinutes(1));
        var game = _env.StartWindowedChild(_playnite, 400, "Ryujinx", _sessionStart.AddMinutes(5));
        _env.ClosesPolitely.Add(game.Id);
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        outcome.ProcessName.ShouldBe("Ryujinx");
        _env.Calls.ShouldNotContain("closeWindow:300:3000");
    }

    [Fact]
    public async Task Jogo_do_launcher_que_nao_pode_ser_identificado_nao_fecha_nada()
    {
        _env.HiddenChildren[_playnite.Id] = 1;
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.NotInspectable);
        _log.Contains(DiagnosticLevel.Warning, "could not be identified").ShouldBeTrue();
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Jogo_do_launcher_que_ignora_o_pedido_e_encerrado_na_segunda_segurada()
    {
        var emulator = _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(5));
        _env.Focus(_playnite);

        var first = await HoldAsync();
        first.Result.ShouldBe(CloseGameResult.AwaitingConfirmation);
        first.ProcessName.ShouldBe("Ryujinx");

        Advance(TimeSpan.FromSeconds(3));
        var second = await HoldAsync();

        second.Result.ShouldBe(CloseGameResult.Forced);
        _env.Calls.ShouldContain("killTree:300");
        _env.Calls.ShouldNotContain("killTree:100");
        _env.Running.ShouldContain(_playnite);
        _env.Running.ShouldNotContain(emulator);
    }

    [Fact]
    public async Task Depois_de_fechar_o_jogo_do_launcher_o_launcher_sozinho_nao_e_alvo()
    {
        var emulator = _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(5));
        _env.ClosesPolitely.Add(emulator.Id);
        _env.Focus(_playnite);
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);

        Advance(CloseGameService.Cooldown + TimeSpan.FromSeconds(1));
        var again = await HoldAsync();

        again.Result.ShouldBe(CloseGameResult.Protected);
        _env.Running.ShouldContain(_playnite);
        _env.Calls.ShouldNotContain("killTree:100");
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
    public async Task Pedido_recusado_pelo_windows_sem_permissao_aponta_a_tela_Geral()
    {
        _env.DeniesClose.Add(_game.Id);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _log.Contains(DiagnosticLevel.Warning, "Allow closing administrator games").ShouldBeTrue();
        _env.Calls.ShouldContain("task:close:200");
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

    [Fact]
    public async Task Jogo_de_administrador_sem_permissao_aponta_a_tela_Geral()
    {
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        outcome.ProcessName.ShouldBe("Hades2");
        _log.Contains(DiagnosticLevel.Warning, "Allow closing administrator games").ShouldBeTrue();
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
        _env.Calls.ShouldNotContain(c => c.StartsWith("wait", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sem_o_fechador_de_administrador_o_jogo_de_administrador_nao_e_tocado()
    {
        var service = new CloseGameService(_env, _env, _settings, new SessionStatusReader(_store), _time, _log);
        _env.AllowTask();
        _env.FocusElevated(_game);

        var outcome = await service.HandleComboAsync(TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _env.Calls.ShouldNotContain(c => c.StartsWith("task", StringComparison.Ordinal));
        _env.Running.ShouldContain(_game);
    }

    [Fact]
    public async Task Jogo_de_administrador_fecha_pela_tarefa()
    {
        _env.AllowTask();
        _env.ClosesPolitely.Add(_game.Id);
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        _env.Calls.ShouldContain("task:close:200");
        _env.Calls.ShouldContain("waitElevated:200:5000");
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
        _env.Calls.ShouldContain("front:Playnite.FullscreenApp");
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.CoolingDown);
    }

    [Fact]
    public async Task Jogo_de_administrador_que_ignora_o_pedido_e_encerrado_pela_tarefa_na_segunda_segurada()
    {
        _env.AllowTask();
        _env.FocusElevated(_game);

        var first = await HoldAsync();
        first.Result.ShouldBe(CloseGameResult.AwaitingConfirmation);
        _service.PendingConfirmation.ShouldBe(_game);

        _env.Focus(_playnite);
        var second = await HoldAsync();

        second.Result.ShouldBe(CloseGameResult.Forced);
        _env.Calls.ShouldContain("task:kill:200");
        _env.Calls.ShouldNotContain(c => c.StartsWith("killTree", StringComparison.Ordinal));
        _env.Calls.ShouldNotContain(c => c.StartsWith("killOne", StringComparison.Ordinal));
        _env.Running.ShouldNotContain(_game);
        _log.Contains(DiagnosticLevel.Info, "through the scheduled task").ShouldBeTrue();
    }

    [Fact]
    public async Task Jogo_de_administrador_encerrado_sem_confirmar_na_primeira_segurada()
    {
        _settings.Current = Settings(force: true);
        _env.AllowTask();
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Forced);
        _env.Calls.ShouldContain("task:close:200");
        _env.Calls.ShouldContain("task:kill:200");
    }

    [Fact]
    public async Task Tarefa_que_recusa_escreve_o_motivo_e_nao_espera()
    {
        _env.AllowTask((int)ElevatedCloseReason.NotStartedBySunshine);
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _log.Contains(DiagnosticLevel.Warning, "not started by Sunshine").ShouldBeTrue();
        _env.Calls.ShouldNotContain(c => c.StartsWith("wait", StringComparison.Ordinal));
        _service.PendingConfirmation.ShouldBeNull();
    }

    [Fact]
    public async Task Tarefa_que_nao_inicia_avisa()
    {
        _env.TaskStatus = ElevatedCloseStatus.Failed;
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _log.Contains(DiagnosticLevel.Warning, "could not start the scheduled task").ShouldBeTrue();
    }

    [Fact]
    public async Task Resposta_da_tarefa_que_nao_chegou_a_tempo_segue_olhando_o_processo()
    {
        _env.TaskStatus = ElevatedCloseStatus.Unknown;
        _env.ClosesPolitely.Add(_game.Id);
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        _log.Contains(DiagnosticLevel.Info, "not read in time").ShouldBeTrue();
    }

    [Fact]
    public async Task Tarefa_que_nao_acha_o_jogo_que_ja_fechou_conta_como_fechado()
    {
        _env.AllowTask((int)ElevatedCloseReason.NotFound);
        _env.FocusElevated(_game);
        _env.Running.Remove(_game);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);
    }

    [Fact]
    public async Task Tarefa_que_nao_acha_o_jogo_ainda_aberto_recusa()
    {
        _env.AllowTask((int)ElevatedCloseReason.NotFound);
        _env.FocusElevated(_game);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AccessDenied);
        _log.Contains(DiagnosticLevel.Warning, "its id now belongs").ShouldBeTrue();
    }

    [Fact]
    public async Task Encerrar_recusado_pela_tarefa_vira_aviso_e_pausa()
    {
        _env.AllowTask();
        _env.FocusElevated(_game);
        await HoldAsync();
        _env.AllowTask((int)ElevatedCloseReason.ActionFailed);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.ForceFailed);
        _log.Contains(DiagnosticLevel.Warning, "Windows refused the close or the kill").ShouldBeTrue();
        (await HoldAsync()).Result.ShouldBe(CloseGameResult.CoolingDown);
    }

    [Fact]
    public async Task Jogo_de_administrador_que_nao_morre_vira_aviso()
    {
        _env.AllowTask();
        _env.Unkillable.Add(_game.Id);
        _env.FocusElevated(_game);
        await HoldAsync();

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.ForceFailed);
        _log.Contains(DiagnosticLevel.Warning, "could not end 'Hades2' (runs as administrator)").ShouldBeTrue();
        _env.Calls.ShouldContain("waitElevated:200:3000");
    }

    [Fact]
    public async Task Jogo_de_administrador_que_sumiu_antes_de_encerrar_conta_como_fechado()
    {
        _env.AllowTask();
        _env.FocusElevated(_game);
        await HoldAsync();
        _env.AllowTask((int)ElevatedCloseReason.NotFound);
        _env.Running.Remove(_game);

        (await HoldAsync()).Result.ShouldBe(CloseGameResult.Closed);
    }

    [Fact]
    public async Task Pedido_recusado_pelo_windows_vai_pela_tarefa_quando_permitido()
    {
        _env.AllowTask();
        _env.DeniesClose.Add(_game.Id);
        _env.ClosesPolitely.Add(_game.Id);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        _env.Calls.ShouldContain("closeWindow:200:2000");
        _env.Calls.ShouldContain("task:close:200");
        _env.Calls.ShouldContain("waitElevated:200:5000");
    }

    [Fact]
    public async Task Jogo_de_administrador_aberto_antes_da_transmissao_fica_protegido()
    {
        var old = _env.Start(400, "OldAdminGame", _sessionStart.AddMinutes(-10));
        _env.AllowTask();
        _env.FocusElevated(old);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.StartedBeforeSession);
        _env.Calls.ShouldNotContain(c => c.StartsWith("task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Launcher_de_administrador_continua_protegido()
    {
        _env.AllowTask();
        _env.FocusElevated(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Protected);
        outcome.Protection.ShouldBe(CloseGameProtectionReason.Launcher);
        _env.Calls.ShouldNotContain(c => c.StartsWith("task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Jogo_de_administrador_aberto_pelo_launcher_fecha_pela_tarefa()
    {
        var admin = _env.Start(500, "AdminGame", _sessionStart.AddMinutes(2));
        _env.ElevatedChildren[_playnite.Id] = [new WindowedProcess(admin, 5000)];
        _env.AllowTask();
        _env.ClosesPolitely.Add(admin.Id);
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.Closed);
        outcome.ProcessName.ShouldBe("AdminGame");
        _env.Calls.ShouldContain("task:close:500");
        _env.Calls.ShouldNotContain(c => c.StartsWith("closeWindow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Launcher_com_jogo_comum_e_jogo_de_administrador_nao_adivinha()
    {
        _env.StartWindowedChild(_playnite, 300, "Ryujinx", _sessionStart.AddMinutes(2));
        var admin = _env.Start(500, "AdminGame", _sessionStart.AddMinutes(3));
        _env.ElevatedChildren[_playnite.Id] = [new WindowedProcess(admin, 5000)];
        _env.AllowTask();
        _env.Focus(_playnite);

        var outcome = await HoldAsync();

        outcome.Result.ShouldBe(CloseGameResult.AmbiguousTarget);
        _env.Calls.ShouldNotContain(c => c.StartsWith("task", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, "done")]
    [InlineData(10, "malformed")]
    [InlineData(11, "is gone")]
    [InlineData(12, "look at the process")]
    [InlineData(13, "Sunshine or SunshineHelper")]
    [InlineData(14, "critical")]
    [InlineData(15, "does not run as administrator")]
    [InlineData(16, "another user")]
    [InlineData(17, "not started by Sunshine")]
    [InlineData(18, "no visible window")]
    [InlineData(20, "refused")]
    [InlineData(30, "unexpected")]
    [InlineData(unchecked((int)0x80070002), "0x80070002")]
    public void Cada_resultado_da_tarefa_tem_um_motivo_no_log(int code, string expected)
    {
        ElevatedCloseReasons.Describe(code).ShouldContain(expected);
    }
}
