using Shouldly;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using Xunit;

namespace SunshineHelper.Application.Tests.Session;

public class SessionOrchestratorDiagnosticsTests
{
    private static (OrchestratorTest Test, ListDiagnosticLog Log, SessionOrchestrator Orchestrator) Build()
    {
        var test = new OrchestratorTest();
        var log = new ListDiagnosticLog();
        var orchestrator = new SessionOrchestrator(test.Modules, test.Store, test.Boot, TimeProvider.System, log);
        return (test, log, orchestrator);
    }

    [Fact]
    public async Task ApplyAsync_registra_o_pedido_e_o_resultado_de_cada_modulo()
    {
        var (_, log, orchestrator) = Build();

        var result = await orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        log.Contains(DiagnosticLevel.Info, "apply start: profile 'tv-4k', resolution 3840x2160").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "apply module 'a': Changed").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "apply module 'c': Changed").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "apply done in").ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyAsync_registra_como_erro_o_modulo_que_falhou()
    {
        var (test, log, orchestrator) = Build();
        test.Modules[1].ApplyFails = "driver said no";

        var result = await orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(SessionApplyStatus.Applied);
        log.Contains(DiagnosticLevel.Error, "apply module 'b': Failed - driver said no").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "revert module 'b': ok").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Warning, "module 'b' put back on its own").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Warning, "with 1 warning(s)").ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyAsync_registra_a_recusa_quando_ja_existe_snapshot()
    {
        var (test, log, orchestrator) = Build();
        test.Store.Content = "{}";

        await orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);

        log.Contains(DiagnosticLevel.Warning, "apply refused").ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreAsync_registra_cada_modulo_restaurado_e_o_caso_sem_snapshot()
    {
        var (_, log, orchestrator) = Build();

        await orchestrator.RestoreAsync(TestContext.Current.CancellationToken);
        log.Contains(DiagnosticLevel.Info, "restore: no snapshot on disk").ShouldBeTrue();

        await orchestrator.ApplyAsync(TestProfiles.FollowClient(), TestProfiles.Request(), TestContext.Current.CancellationToken);
        await orchestrator.RestoreAsync(TestContext.Current.CancellationToken);

        log.Contains(DiagnosticLevel.Info, "restore start: 3 module(s) to revert").ShouldBeTrue();
        log.Contains(DiagnosticLevel.Info, "revert module 'c': ok").ShouldBeTrue();
    }
}
