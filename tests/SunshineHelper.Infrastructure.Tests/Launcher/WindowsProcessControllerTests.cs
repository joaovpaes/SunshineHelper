using System.Diagnostics;
using Shouldly;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Infrastructure.Launcher;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Launcher;

/// <summary>Runs against real processes: a throwaway <c>ping</c> that would live for two
/// minutes, started and ended by the test itself. Nothing else on the machine is touched.</summary>
public sealed class WindowsProcessControllerTests : IDisposable
{
    private readonly WindowsProcessController _controller = new();
    private readonly List<Process> _started = [];

    [Fact]
    public void FindByNames_acha_o_processo_pelo_nome_com_pid_e_horario_de_inicio()
    {
        var process = StartSleeper();

        var found = _controller.FindByNames(["ping"]).Where(identity => identity.Id == process.Id).ToList();

        var identity = found.ShouldHaveSingleItem();
        identity.StartedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-2));
    }

    [Fact]
    public void FindByNames_com_nome_inexistente_devolve_vazio()
    {
        _controller.FindByNames(["sunshinehelper-nao-existe-xyz"]).ShouldBeEmpty();
    }

    [Fact]
    public async Task KillTreeAsync_encerra_o_processo_e_confirma()
    {
        var process = StartSleeper();
        var identity = IdentityOf(process);

        bool gone = await _controller.KillTreeAsync(identity, TestContext.Current.CancellationToken);

        gone.ShouldBeTrue();
        process.HasExited.ShouldBeTrue();
    }

    [Fact]
    public async Task Identidade_com_horario_diferente_nao_encerra_nada()
    {
        // Same PID, but a start time from long ago: as if the PID had been recycled.
        var process = StartSleeper();
        var stale = IdentityOf(process) with { StartedAt = DateTimeOffset.UtcNow.AddHours(-3) };

        _controller.RequestClose(stale).ShouldBeFalse();
        (await _controller.KillTreeAsync(stale, TestContext.Current.CancellationToken)).ShouldBeTrue();

        process.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void RequestClose_em_processo_sem_janela_devolve_false_sem_encerrar()
    {
        var process = StartSleeper();

        _controller.RequestClose(IdentityOf(process)).ShouldBeFalse();

        process.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task WaitForExitAsync_respeita_o_teto_quando_o_processo_continua()
    {
        var process = StartSleeper();

        bool exited = await _controller.WaitForExitAsync(
            IdentityOf(process), TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        exited.ShouldBeFalse();
    }

    [Fact]
    public async Task Processo_que_ja_nao_existe_conta_como_encerrado()
    {
        var process = StartSleeper();
        var identity = IdentityOf(process);
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        (await _controller.WaitForExitAsync(identity, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await _controller.KillTreeAsync(identity, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            process.Dispose();
        }
    }

    private ProcessIdentity IdentityOf(Process process) =>
        _controller.FindByNames(["ping"]).Single(identity => identity.Id == process.Id);

    private Process StartSleeper()
    {
        var process = Process.Start(new ProcessStartInfo("ping", "-n 120 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        }) ?? throw new InvalidOperationException("ping did not start.");

        _started.Add(process);
        return process;
    }
}
