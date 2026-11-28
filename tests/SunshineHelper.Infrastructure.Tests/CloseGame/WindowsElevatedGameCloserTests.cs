using System.Diagnostics;
using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Infrastructure.CloseGame;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.CloseGame;

/// <summary>What can run on the CI runner without the scheduled task: nothing here registers or
/// starts it. The wait reads the system's process list, the same read that identifies a game
/// running as administrator, so these tests also check that read against .NET's own values.</summary>
public sealed class WindowsElevatedGameCloserTests
{
    private readonly WindowsElevatedGameCloser _closer = new();

    [Fact]
    public async Task O_proprio_processo_e_achado_na_lista_do_sistema_com_o_mesmo_inicio()
    {
        ProcessIdentity self = Self();

        bool gone = await _closer.WaitForExitAsync(self, TimeSpan.Zero, TestContext.Current.CancellationToken);

        gone.ShouldBeFalse();
    }

    [Fact]
    public async Task Mesmo_PID_com_outro_inicio_conta_como_outro_processo()
    {
        ProcessIdentity self = Self();
        ProcessIdentity other = self with { StartedAt = self.StartedAt.AddTicks(1) };

        bool gone = await _closer.WaitForExitAsync(other, TimeSpan.Zero, TestContext.Current.CancellationToken);

        gone.ShouldBeTrue();
    }

    [Fact]
    public async Task Processo_que_terminou_conta_como_fechado()
    {
        using Process ping = Process.Start(new ProcessStartInfo("ping", "-n 2 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        var identity = new ProcessIdentity(ping.Id, ping.ProcessName, new DateTimeOffset(ping.StartTime));
        await ping.WaitForExitAsync(TestContext.Current.CancellationToken);

        bool gone = await _closer.WaitForExitAsync(identity, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        gone.ShouldBeTrue();
    }

    [Fact]
    public async Task Sem_a_tarefa_registrada_a_permissao_nao_existe()
    {
        ProcessIdentity self = Self();

        ElevatedCloseAttempt attempt = await _closer.RunAsync(ElevatedCloseOperation.Close, self, TestContext.Current.CancellationToken);

        // The runner never has the task (no test installs it), so nothing is started.
        attempt.Status.ShouldBe(ElevatedCloseStatus.NotAllowed);
    }

    private static ProcessIdentity Self()
    {
        using Process self = Process.GetCurrentProcess();
        return new ProcessIdentity(self.Id, self.ProcessName, new DateTimeOffset(self.StartTime));
    }
}
