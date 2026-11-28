using Shouldly;
using SunshineHelper.Application.CloseGame;
using Xunit;

namespace SunshineHelper.Application.Tests.CloseGame;

public sealed class ElevatedCloseSetupStateTests
{
    [Fact]
    public void Nada_instalado_numa_conta_de_administrador()
    {
        Facts(installed: false, task: false).Decide().ShouldBe(ElevatedCloseSetupState.NotAllowed);
    }

    [Fact]
    public void Instalado_e_igual_ao_do_app()
    {
        Facts(installed: true, task: true, same: true).Decide().ShouldBe(ElevatedCloseSetupState.Allowed);
    }

    [Fact]
    public void Instalado_mas_diferente_do_app_pede_atualizar()
    {
        Facts(installed: true, task: true, same: false).Decide().ShouldBe(ElevatedCloseSetupState.Outdated);
    }

    [Fact]
    public void Instalado_sem_o_auxiliar_ao_lado_do_app_continua_permitido()
    {
        Facts(bundled: false, installed: true, task: true, same: false).Decide().ShouldBe(ElevatedCloseSetupState.Allowed);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Instalacao_pela_metade_conta_como_nao_permitido(bool installed, bool task)
    {
        Facts(installed: installed, task: task).Decide().ShouldBe(ElevatedCloseSetupState.NotAllowed);
    }

    [Fact]
    public void Conta_sem_administrador()
    {
        Facts(admin: false).Decide().ShouldBe(ElevatedCloseSetupState.NotAdminAccount);
    }

    [Fact]
    public void Conta_sem_administrador_mas_ja_permitido_segue_permitido()
    {
        Facts(admin: false, installed: true, task: true, same: true).Decide().ShouldBe(ElevatedCloseSetupState.Allowed);
    }

    [Fact]
    public void Sem_o_auxiliar_ao_lado_do_app()
    {
        Facts(bundled: false).Decide().ShouldBe(ElevatedCloseSetupState.AuxiliaryMissing);
    }

    private static ElevatedCloseSetupFacts Facts(
        bool admin = true, bool bundled = true, bool installed = false, bool task = false, bool same = false) =>
        new(admin, bundled, installed, task, same);
}
