using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Controller;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Tests.Channel;
using SunshineHelper.Application.Tests.Session;
using SunshineHelper.Domain.Gamepad;
using SunshineHelper.Domain.Settings;
using Xunit;

namespace SunshineHelper.Application.Tests.Controller;

public class ControllerWatcherTests
{
    private const GamepadButtons SelectY = GamepadButtons.Back | GamepadButtons.Y;

    private readonly ManualTimeProvider _time = new();
    private readonly ScriptedGamepadReader _reader = new();
    private readonly FakeSessionSnapshotStore _store = new();
    private readonly ListDiagnosticLog _log = new();
    private readonly FakeCurrentSettingsProvider _settings = new(WithCloseGame(enabled: true));
    private readonly List<(string Id, int Index)> _fired = [];
    private readonly ControllerWatcher _watcher;

    public ControllerWatcherTests()
    {
        _watcher = new ControllerWatcher(_reader, _store, _settings, _time, _log);
        _watcher.ComboFired += (combo, index) => _fired.Add((combo.Id, index));
    }

    private static AppSettings WithCloseGame(bool enabled) =>
        AppSettings.CreateDefault() with
        {
            Controller = new ControllerSettings
            {
                CloseGame = new CloseGameSettings { Enabled = enabled },

                // These tests are about the close-game combo; the overlay combo has its own.
                Overlay = new OverlaySettings { Enabled = false },
            },
        };

    private void StartSession() => _store.Content = "{}";

    private void EndSession() => _store.Content = null;

    /// <summary>Runs passes every 16 ms for <paramref name="milliseconds"/>.</summary>
    private void Run(int milliseconds)
    {
        for (int elapsed = 0; elapsed < milliseconds; elapsed += 16)
        {
            _watcher.Step();
            _time.AdvanceMilliseconds(16);
        }
    }

    [Fact]
    public void Sem_sessao_nao_le_o_controle()
    {
        _reader.Connect(0, SelectY);

        Run(6000);

        _reader.Reads.ShouldBe(0);
        _fired.ShouldBeEmpty();
    }

    [Fact]
    public void Com_fechar_o_jogo_desligado_nao_le_o_controle_nem_na_sessao()
    {
        _settings.Current = WithCloseGame(enabled: false);
        StartSession();
        _reader.Connect(0, SelectY);

        Run(6000);

        _reader.Reads.ShouldBe(0);
        _fired.ShouldBeEmpty();
    }

    [Fact]
    public void Na_sessao_segurar_select_y_por_5_segundos_dispara_uma_vez()
    {
        StartSession();
        _reader.Connect(0, SelectY);

        Run(4900);
        _fired.ShouldBeEmpty();

        Run(1200);
        _fired.Count.ShouldBe(1);
        _fired[0].Id.ShouldBe(ControllerSettings.CloseGameComboId);
        _fired[0].Index.ShouldBe(0);

        Run(10_000);
        _fired.Count.ShouldBe(1);
    }

    [Fact]
    public void Dispara_escreve_uma_linha_no_log()
    {
        StartSession();
        _reader.Connect(2, SelectY);

        Run(5500);

        _log.Contains(DiagnosticLevel.Info, "combo 'closeGame' fired on controller 2").ShouldBeTrue();
        _log.Contains(DiagnosticLevel.Info, "controller 2 connected").ShouldBeTrue();
    }

    [Fact]
    public void Soltar_antes_do_tempo_nao_dispara()
    {
        StartSession();
        _reader.Connect(0, SelectY);
        Run(3000);

        _reader.Connect(0, GamepadButtons.None);
        Run(3000);
        _reader.Connect(0, SelectY);
        Run(3000);

        _fired.ShouldBeEmpty();
    }

    [Fact]
    public void Controle_desligado_no_meio_cancela_e_e_registrado()
    {
        StartSession();
        _reader.Connect(0, SelectY);
        Run(3000);

        _reader.Disconnect(0);
        Run(2000);
        _reader.Connect(0, SelectY);
        Run(4000);

        _fired.ShouldBeEmpty();
        _log.Contains(DiagnosticLevel.Info, "controller 0 disconnected").ShouldBeTrue();
    }

    [Fact]
    public void Fim_da_sessao_para_de_ler()
    {
        StartSession();
        _reader.Connect(0, GamepadButtons.None);
        Run(1500);
        EndSession();
        Run(1500);

        int readsAtEnd = _reader.Reads;
        Run(5000);

        _reader.Reads.ShouldBe(readsAtEnd);
    }

    [Fact]
    public void Fim_da_sessao_avisa_uma_vez()
    {
        int ended = 0;
        _watcher.SessionEnded += () => ended++;
        StartSession();
        Run(2000);
        ended.ShouldBe(0);

        EndSession();
        Run(3000);

        ended.ShouldBe(1);
    }

    [Fact]
    public void Nova_sessao_conta_do_zero()
    {
        StartSession();
        _reader.Connect(0, SelectY);
        Run(3000);
        EndSession();
        Run(2000);

        StartSession();
        Run(3000);
        _fired.ShouldBeEmpty();

        // A sessão nova pode levar até 1 s para ser notada, então a margem é folgada.
        Run(3500);
        _fired.Count.ShouldBe(1);
    }

    [Fact]
    public void Ligar_o_combo_no_meio_da_sessao_vale_sem_reiniciar()
    {
        _settings.Current = WithCloseGame(enabled: false);
        StartSession();
        _reader.Connect(0, SelectY);
        Run(2000);

        _settings.Current = WithCloseGame(enabled: true);
        Run(5500);

        _fired.Count.ShouldBe(1);
    }

    [Fact]
    public void Slot_vazio_so_e_consultado_uma_vez_por_segundo()
    {
        StartSession();

        Run(10_000);

        // Quatro slots vazios: uma consulta cada, mais ou menos, a cada segundo.
        _reader.Reads.ShouldBeLessThan(4 * 12);
        _reader.Reads.ShouldBeGreaterThanOrEqualTo(4 * 9);
    }

    [Fact]
    public void Controle_ligado_depois_e_visto_em_ate_um_segundo()
    {
        StartSession();
        Run(3000);

        _reader.Connect(1, SelectY);
        Run(1100);
        _log.Contains(DiagnosticLevel.Info, "controller 1 connected").ShouldBeTrue();
    }

    [Fact]
    public void Cada_controle_dispara_pelo_proprio_indice()
    {
        StartSession();
        _reader.Connect(1, GamepadButtons.A);
        _reader.Connect(3, SelectY);

        Run(6000);

        _fired.Count.ShouldBe(1);
        _fired[0].Index.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_termina_quando_cancelado()
    {
        var watcher = new ControllerWatcher(_reader, _store, _settings);
        using var cts = new CancellationTokenSource();
        var running = watcher.RunAsync(cts.Token);

        await cts.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        running.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_registra_erro_do_leitor_e_continua()
    {
        StartSession();
        _reader.ThrowOnRead = new InvalidOperationException("boom");
        var log = new ListDiagnosticLog();
        var watcher = new ControllerWatcher(_reader, _store, _settings, TimeProvider.System, log);
        using var cts = new CancellationTokenSource();

        var running = watcher.RunAsync(cts.Token);
        for (int i = 0; i < 100 && !log.Contains(DiagnosticLevel.Error, "boom"); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await cts.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        log.Contains(DiagnosticLevel.Error, "boom").ShouldBeTrue();
    }

    [Fact]
    public void Construtor_recusa_nulos()
    {
        Should.Throw<ArgumentNullException>(() => new ControllerWatcher(null!, _store, _settings));
        Should.Throw<ArgumentNullException>(() => new ControllerWatcher(_reader, null!, _settings));
        Should.Throw<ArgumentNullException>(() => new ControllerWatcher(_reader, _store, null!));
    }
}

/// <summary>A clock the test moves by hand. It overrides the timestamp, not the UTC time,
/// because the watcher measures elapsed time with <c>GetElapsedTime</c>.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _milliseconds;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _milliseconds;

    public void AdvanceMilliseconds(long milliseconds) => _milliseconds += milliseconds;
}

/// <summary>Controllers the test plugs in and out; counts how many times it was asked.</summary>
internal sealed class ScriptedGamepadReader : IGamepadReader
{
    private readonly GamepadSample[] _slots = new GamepadSample[ComboDetector.MaxControllers];

    public int Reads { get; private set; }

    public Exception? ThrowOnRead { get; set; }

    public void Connect(int index, GamepadButtons buttons) => _slots[index] = new GamepadSample(true, buttons);

    public void Disconnect(int index) => _slots[index] = default;

    public GamepadSample Read(int index)
    {
        Reads++;
        if (ThrowOnRead is not null)
        {
            throw ThrowOnRead;
        }

        return _slots[index];
    }
}
