using System.Diagnostics;
using System.Globalization;
using Shouldly;
using SunshineHelper.Trigger.ElevatedClose;
using Xunit;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

/// <summary>Runs against a throwaway <c>ping</c> started by the test itself; nothing else on the
/// machine is touched. Whether a process counts as elevated depends on how the runner is set up, so
/// that answer is read but not asserted.</summary>
public sealed class WindowsElevatedProcessSystemTests : IDisposable
{
    // A process id Windows never hands out in practice (ids are small multiples of 4).
    private const int MissingProcessId = 0x7FFF_FFFC;

    private readonly WindowsElevatedProcessSystem _system = new();
    private readonly List<Process> _started = [];

    [Fact]
    public void Open_le_nome_pai_e_inicio_do_processo()
    {
        var ping = StartPing();

        _system.Open(ping.Id, forKill: false, out ITargetProcess? opened).ShouldBe(OpenStatus.Opened);
        opened.ShouldNotBeNull();
        using ITargetProcess target = opened;

        target.Node.ProcessId.ShouldBe(ping.Id);
        target.Node.Name.ShouldBe("ping", StringCompareShould.IgnoreCase);
        target.Node.ParentProcessId.ShouldBe(Environment.ProcessId);
        target.Node.StartTime.ShouldBe(StartTimeOf(ping));
        target.BelongsToCaller.ShouldBeTrue();
        target.IsCritical.ShouldBeFalse();
        _ = target.IsElevated;
    }

    [Fact]
    public void Processo_sem_janela_nao_tem_janela_nem_aceita_pedido_de_fechar()
    {
        var ping = StartPing();

        _system.Open(ping.Id, forKill: false, out ITargetProcess? opened).ShouldBe(OpenStatus.Opened);
        opened.ShouldNotBeNull();
        using ITargetProcess target = opened;

        target.HasVisibleWindow.ShouldBeFalse();
        target.RequestClose().ShouldBeFalse();
        ping.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void Kill_encerra_o_processo()
    {
        var ping = StartPing();

        _system.Open(ping.Id, forKill: true, out ITargetProcess? opened).ShouldBe(OpenStatus.Opened);
        opened.ShouldNotBeNull();
        using ITargetProcess target = opened;

        target.Kill().ShouldBeTrue();
        ping.WaitForExit(5_000).ShouldBeTrue();
    }

    [Fact]
    public void Kill_sem_o_direito_de_encerrar_nao_encerra()
    {
        var ping = StartPing();

        _system.Open(ping.Id, forKill: false, out ITargetProcess? opened).ShouldBe(OpenStatus.Opened);
        opened.ShouldNotBeNull();
        using ITargetProcess target = opened;

        target.Kill().ShouldBeFalse();
        ping.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void Open_de_processo_que_nao_existe()
    {
        _system.Open(MissingProcessId, forKill: false, out ITargetProcess? opened).ShouldBe(OpenStatus.NotFound);

        opened.ShouldBeNull();
    }

    [Fact]
    public void Describe_le_o_proprio_processo()
    {
        using var self = Process.GetCurrentProcess();

        ProcessNode? node = _system.Describe(Environment.ProcessId);

        node.ShouldNotBeNull();
        node.ProcessId.ShouldBe(Environment.ProcessId);
        node.Name.ShouldBe(self.ProcessName, StringCompareShould.IgnoreCase);
        node.StartTime.ShouldBe(StartTimeOf(self));
    }

    [Fact]
    public void Describe_de_processo_que_nao_existe_devolve_nulo()
    {
        _system.Describe(MissingProcessId).ShouldBeNull();
    }

    [Fact]
    public void O_modo_inteiro_recusa_um_processo_que_nao_veio_do_Sunshine()
    {
        var ping = StartPing();
        string id = ping.Id.ToString(CultureInfo.InvariantCulture);
        string start = StartTimeOf(ping).ToString(CultureInfo.InvariantCulture);

        int exitCode = ElevatedCloseMode.Run(["elevated-close", "kill", id, start]);

        // On a runner without elevation the answer is "not elevated"; with it, "not started by Sunshine".
        exitCode.ShouldBeOneOf((int)ElevatedCloseExitCode.NotElevated, (int)ElevatedCloseExitCode.NotStartedBySunshine);
        ping.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void O_modo_inteiro_recusa_outro_horario_de_inicio()
    {
        var ping = StartPing();
        string id = ping.Id.ToString(CultureInfo.InvariantCulture);
        string start = (StartTimeOf(ping) + 1).ToString(CultureInfo.InvariantCulture);

        ElevatedCloseMode.Run(["elevated-close", "kill", id, start]).ShouldBe((int)ElevatedCloseExitCode.NotFound);
        ping.HasExited.ShouldBeFalse();
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5_000);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            process.Dispose();
        }
    }

    private static long StartTimeOf(Process process) => process.StartTime.ToUniversalTime().ToFileTimeUtc();

    private Process StartPing()
    {
        var info = new ProcessStartInfo("ping", "-n 60 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        var process = Process.Start(info) ?? throw new InvalidOperationException("ping did not start");
        _started.Add(process);
        return process;
    }
}
