using System.Diagnostics;
using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Application.Launcher;
using SunshineHelper.Infrastructure.CloseGame;
using SunshineHelper.Infrastructure.Launcher;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.CloseGame;

/// <summary>Runs against real processes started by the test itself (a throwaway
/// <c>cmd</c> that starts a <c>ping</c>). Nothing else on the machine is touched. What
/// depends on a desktop (which window is in the foreground, bringing a window to the front)
/// is only checked for not throwing: a CI runner has no one at the screen.</summary>
public sealed class WindowsGameWindowControllerTests : IDisposable
{
    private readonly WindowsGameWindowController _controller = new();
    private readonly WindowsProcessController _processes = new();
    private readonly List<Process> _started = [];

    [Fact]
    public void GetForeground_nao_lanca_e_devolve_um_estado_valido()
    {
        var foreground = _controller.GetForeground();

        Enum.IsDefined(foreground.Status).ShouldBeTrue();
        if (foreground.Status == ForegroundStatus.Found)
        {
            foreground.Process.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GetDescendants_acha_o_filho_do_processo()
    {
        var parent = StartTree();
        var parentIdentity = IdentityOf(parent, "cmd");

        var children = await WaitForChildrenAsync(parentIdentity);

        children.ShouldContain(child => child.Name.Equals("ping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetDescendants_de_processo_que_nao_existe_devolve_vazio()
    {
        var parent = StartTree();
        var stale = IdentityOf(parent, "cmd") with { StartedAt = DateTimeOffset.UtcNow.AddHours(-3) };

        _controller.GetDescendants(stale).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetWindowedDescendants_ignora_filhos_sem_janela()
    {
        var parent = StartTree();
        var parentIdentity = IdentityOf(parent, "cmd");
        await WaitForChildrenAsync(parentIdentity);

        var launched = _controller.GetWindowedDescendants(parentIdentity);

        // ping and the console host run without a visible window.
        launched.Windows.ShouldBeEmpty();
    }

    [Fact]
    public void GetWindowedDescendants_de_processo_que_nao_existe_devolve_vazio()
    {
        var parent = StartTree();
        var stale = IdentityOf(parent, "cmd") with { StartedAt = DateTimeOffset.UtcNow.AddHours(-3) };

        _controller.GetWindowedDescendants(stale).ShouldBe(LaunchedWindows.None);
    }

    [Fact]
    public async Task KillProcessAsync_encerra_so_o_pai_e_deixa_o_filho()
    {
        var parent = StartTree();
        var parentIdentity = IdentityOf(parent, "cmd");
        var children = await WaitForChildrenAsync(parentIdentity);
        var child = children.First(c => c.Name.Equals("ping", StringComparison.OrdinalIgnoreCase));

        bool gone = await _controller.KillProcessAsync(parentIdentity, TestContext.Current.CancellationToken);

        gone.ShouldBeTrue();
        parent.HasExited.ShouldBeTrue();
        using var survivor = Process.GetProcessById(child.Id);
        try
        {
            survivor.HasExited.ShouldBeFalse();
        }
        finally
        {
            survivor.Kill();
        }
    }

    [Fact]
    public async Task KillProcessAsync_com_horario_diferente_nao_encerra_nada()
    {
        var parent = StartTree();
        var stale = IdentityOf(parent, "cmd") with { StartedAt = DateTimeOffset.UtcNow.AddHours(-3) };

        bool gone = await _controller.KillProcessAsync(stale, TestContext.Current.CancellationToken);

        gone.ShouldBeTrue();
        parent.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void RequestCloseWindow_com_identidade_velha_nao_faz_nada()
    {
        var parent = StartTree();
        var stale = IdentityOf(parent, "cmd") with { StartedAt = DateTimeOffset.UtcNow.AddHours(-3) };

        _controller.RequestCloseWindow(stale, 12345).ShouldBe(WindowCloseResult.Gone);

        parent.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void RequestCloseWindow_com_janela_que_nao_e_do_processo_devolve_gone()
    {
        var parent = StartTree();

        _controller.RequestCloseWindow(IdentityOf(parent, "cmd"), 1).ShouldBe(WindowCloseResult.Gone);

        parent.HasExited.ShouldBeFalse();
    }

    [Fact]
    public void BringToFront_de_processo_sem_janela_devolve_false()
    {
        var parent = StartTree();

        _controller.BringToFront(IdentityOf(parent, "cmd")).ShouldBeFalse();
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

    private async Task<List<ProcessIdentity>> WaitForChildrenAsync(ProcessIdentity parent)
    {
        // The ping starts a moment after cmd does (and cmd's console host shows up first).
        var children = new List<ProcessIdentity>();
        for (int attempt = 0; attempt < 50; attempt++)
        {
            children = _controller.GetDescendants(parent).ToList();
            if (children.Any(child => child.Name.Equals("ping", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return children;
    }

    private ProcessIdentity IdentityOf(Process process, string name) =>
        _processes.FindByNames([name]).Single(identity => identity.Id == process.Id);

    private Process StartTree()
    {
        var process = Process.Start(new ProcessStartInfo("cmd", "/c ping -n 120 127.0.0.1 > nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("cmd did not start.");

        _started.Add(process);
        return process;
    }
}
