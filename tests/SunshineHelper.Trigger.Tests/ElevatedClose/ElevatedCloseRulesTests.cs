using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Trigger.ElevatedClose;
using Xunit;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

/// <summary>The rules of section 3 of ADR 0017, on a made-up process list: services (50) started
/// sunshine (100), which started Playnite (200), which started the game (300).</summary>
public sealed class ElevatedCloseRulesTests
{
    private const int GameId = 300;
    private const long GameStart = 3_000;

    private static readonly string[] _closed = ["close"];
    private static readonly string[] _killed = ["kill"];

    private readonly FakeProcessSystem _system = new();
    private readonly FakeTarget _game;

    public ElevatedCloseRulesTests()
    {
        _system.Add(new ProcessNode(50, 4, "services", 500));
        _system.Add(new ProcessNode(100, 50, "sunshine", 1_000));
        _system.Add(new ProcessNode(200, 100, "Playnite.FullscreenApp", 2_000));
        _game = new FakeTarget(new ProcessNode(GameId, 200, "SuperOdysseyRecomp", GameStart));
        _system.Target = _game;
    }

    [Fact]
    public void Fecha_o_jogo_elevado_aberto_pelo_Sunshine()
    {
        Run(ElevatedCloseOperation.Close).ShouldBe(ElevatedCloseExitCode.Done);

        _game.Actions.ShouldBe(_closed);
        _system.OpenedForKill.ShouldBe(false);
        _game.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void Encerra_o_jogo_e_pede_o_direito_de_encerrar()
    {
        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.Done);

        _game.Actions.ShouldBe(_killed);
        _system.OpenedForKill.ShouldBe(true);
    }

    [Fact]
    public void Windows_recusar_a_acao_vira_falha()
    {
        _game.ActionSucceeds = false;

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.ActionFailed);
    }

    [Fact]
    public void Processo_que_nao_existe()
    {
        Run(ElevatedCloseOperation.Close, processId: 999).ShouldBe(ElevatedCloseExitCode.NotFound);
    }

    [Fact]
    public void Processo_que_o_Windows_nao_deixa_abrir()
    {
        _system.OpenResult = OpenStatus.Denied;

        Run(ElevatedCloseOperation.Close).ShouldBe(ElevatedCloseExitCode.NotInspectable);
    }

    [Fact]
    public void Mesmo_PID_com_outro_horario_de_inicio_e_outro_processo()
    {
        Run(ElevatedCloseOperation.Kill, startTime: GameStart + 1).ShouldBe(ElevatedCloseExitCode.NotFound);

        _game.Actions.ShouldBeEmpty();
        _game.Disposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("SunshineHelper")]
    [InlineData("SunshineHelper.Trigger.exe")]
    [InlineData("Sunshine")]
    [InlineData("ApplicationFrameHost")]
    [InlineData("svchost.exe")]
    [InlineData("")]
    public void Nomes_proibidos_nunca_sao_fechados(string name)
    {
        var target = Elevated(name);
        _system.Target = target;

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.ForbiddenName);

        target.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Processo_critico_e_recusado()
    {
        _game.IsCritical = true;

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.Critical);
        _game.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Processo_comum_e_recusado_porque_o_app_consegue_sozinho()
    {
        _game.IsElevated = false;

        Run(ElevatedCloseOperation.Close).ShouldBe(ElevatedCloseExitCode.NotElevated);
        _game.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Processo_de_outro_usuario_ou_sessao_e_recusado()
    {
        _game.BelongsToCaller = false;

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.OtherUser);
        _game.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Processo_aberto_fora_do_Sunshine_e_recusado()
    {
        _system.Add(new ProcessNode(100, 50, "explorer", 1_000));

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.NotStartedBySunshine);
        _game.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Sem_janela_visivel_e_recusado()
    {
        _game.HasVisibleWindow = false;

        Run(ElevatedCloseOperation.Close).ShouldBe(ElevatedCloseExitCode.NoVisibleWindow);
        _game.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Sunshine_como_pai_direto_vale()
    {
        _system.Target = new FakeTarget(new ProcessNode(GameId, 100, "game", GameStart));

        Run(ElevatedCloseOperation.Close).ShouldBe(ElevatedCloseExitCode.Done);
    }

    [Fact]
    public void Pai_que_comecou_depois_do_filho_e_PID_reaproveitado()
    {
        // The real Playnite exited; a newer process got id 200 and happens to descend from Sunshine.
        _system.Add(new ProcessNode(200, 100, "Playnite.FullscreenApp", GameStart + 10));

        ElevatedCloseRules.IsStartedBySunshine(_game.Node, _system).ShouldBeFalse();
    }

    [Fact]
    public void Um_sunshine_falso_mais_novo_que_o_filho_nao_conta()
    {
        _system.Target = new FakeTarget(new ProcessNode(GameId, 900, "game", GameStart));
        _system.Add(new ProcessNode(900, 50, "sunshine", GameStart + 1));

        Run(ElevatedCloseOperation.Kill).ShouldBe(ElevatedCloseExitCode.NotStartedBySunshine);
    }

    [Fact]
    public void Pai_que_nao_existe_mais_interrompe_a_subida()
    {
        _system.Nodes.Remove(200);

        ElevatedCloseRules.IsStartedBySunshine(_game.Node, _system).ShouldBeFalse();
    }

    [Fact]
    public void Pai_desconhecido_interrompe_a_subida()
    {
        ElevatedCloseRules.IsStartedBySunshine(new ProcessNode(GameId, 0, "game", GameStart), _system).ShouldBeFalse();
    }

    [Fact]
    public void Ciclo_de_pais_nao_trava()
    {
        _system.Add(new ProcessNode(200, 201, "a", 2_000));
        _system.Add(new ProcessNode(201, 200, "b", 2_000));

        ElevatedCloseRules.IsStartedBySunshine(_game.Node, _system).ShouldBeFalse();
    }

    [Fact]
    public void Descricao_com_outro_PID_nao_conta()
    {
        _system.Nodes[200] = new ProcessNode(201, 100, "Playnite.FullscreenApp", 2_000);

        ElevatedCloseRules.IsStartedBySunshine(_game.Node, _system).ShouldBeFalse();
    }

    [Fact]
    public void Arvore_mais_funda_que_o_limite_nao_e_aceita()
    {
        // A chain of MaxAncestryDepth + 1 levels with Sunshine at the very top.
        int top = 1_000 + ElevatedCloseRules.MaxAncestryDepth + 1;
        _system.Add(new ProcessNode(top, 50, "sunshine", 1));
        for (int id = 1_000; id < top; id++)
        {
            _system.Add(new ProcessNode(id, id + 1, "step", 2));
        }

        var deep = new ProcessNode(GameId, 1_000, "game", GameStart);

        ElevatedCloseRules.IsStartedBySunshine(deep, _system).ShouldBeFalse();
    }

    [Fact]
    public void Arvore_dentro_do_limite_e_aceita()
    {
        int top = 1_000 + ElevatedCloseRules.MaxAncestryDepth - 1;
        _system.Add(new ProcessNode(top, 50, "sunshine.exe", 1));
        for (int id = 1_000; id < top; id++)
        {
            _system.Add(new ProcessNode(id, id + 1, "step", 2));
        }

        var deep = new ProcessNode(GameId, 1_000, "game", GameStart);

        ElevatedCloseRules.IsStartedBySunshine(deep, _system).ShouldBeTrue();
    }

    [Fact]
    public void A_lista_fixa_cobre_todos_os_nomes_protegidos_do_app()
    {
        foreach (string name in LauncherSettings.ForbiddenNames)
        {
            ElevatedCloseRules.ForbiddenNames.ShouldContain(name);
        }

        foreach (string name in CloseGameProtection.ExtraFixedNames)
        {
            ElevatedCloseRules.ForbiddenNames.ShouldContain(name);
        }
    }

    private ElevatedCloseExitCode Run(ElevatedCloseOperation operation, int processId = GameId, long startTime = GameStart) =>
        ElevatedCloseRules.Run(new ElevatedCloseRequest(operation, processId, startTime), _system);

    private static FakeTarget Elevated(string name) => new(new ProcessNode(GameId, 200, name, GameStart));
}
