using System.Globalization;
using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Infrastructure.CloseGame;
using SunshineHelper.Protocol;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.CloseGame;

/// <summary>Only what does not install anything: the CI runner is an administrator with UAC off, so
/// a real "allow" there would silently register the task on that machine.</summary>
public sealed class WindowsElevatedCloseSetupTests
{
    private static readonly string _missing = Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), ElevatedCloseContract.ExecutableName);

    [Fact]
    public void Por_padrao_usa_o_auxiliar_ao_lado_do_app()
    {
        new WindowsElevatedCloseSetup().BundledPath.ShouldBe(Path.Combine(AppContext.BaseDirectory, ElevatedCloseContract.ExecutableName));
    }

    [Fact]
    public void Copia_instalada_fica_em_Program_Files()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        WindowsElevatedCloseSetup.InstalledPath.ShouldBe(Path.Combine(programFiles, "SunshineHelper", "SunshineHelper.Trigger.exe"));
    }

    [Fact]
    public async Task Sem_o_auxiliar_ao_lado_do_app_nao_ha_o_que_instalar()
    {
        var setup = new WindowsElevatedCloseSetup(_missing);

        ElevatedCloseSetupState state = await setup.GetStateAsync(TestContext.Current.CancellationToken);

        // Nothing is installed on the runner; a runner account without administrator would say so first.
        state.ShouldBeOneOf(ElevatedCloseSetupState.AuxiliaryMissing, ElevatedCloseSetupState.NotAdminAccount);
    }

    [Fact]
    public async Task Permitir_sem_o_auxiliar_falha_sem_pedir_UAC()
    {
        var setup = new WindowsElevatedCloseSetup(_missing);

        ElevatedCloseSetupResult result = await setup.InstallAsync(TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ElevatedCloseSetupOutcome.Failed);
        result.ExitCode.ShouldBeNull();
    }

    [Fact]
    public async Task Remover_sem_o_auxiliar_falha_sem_pedir_UAC()
    {
        var setup = new WindowsElevatedCloseSetup(_missing);

        ElevatedCloseSetupResult result = await setup.UninstallAsync(TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ElevatedCloseSetupOutcome.Failed);
    }

    [Fact]
    public void Ler_se_a_conta_e_de_administrador_nao_falha()
    {
        _ = WindowsElevatedCloseSetup.IsAdminAccount();
    }
}
