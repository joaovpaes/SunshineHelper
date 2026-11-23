using Microsoft.Win32;
using Shouldly;
using SunshineHelper.Infrastructure.Startup;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Startup;

// Uses a throwaway key under HKCU, never the real Run key, and removes it afterwards.
public sealed class WindowsStartupRegistrationTests : IDisposable
{
    private const string ExePath = @"C:\Program Files\SunshineHelper\SunshineHelper.exe";

    private readonly string _keyPath = string.Concat(@"Software\SunshineHelper.Tests\", Guid.NewGuid().ToString("N"));

    private WindowsStartupRegistration Create() => new(_keyPath, "SunshineHelperTest");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    [Fact]
    public void Comeca_desligado()
    {
        Create().IsEnabled().ShouldBeFalse();
    }

    [Fact]
    public void Ligar_e_desligar_muda_o_estado()
    {
        var registration = Create();

        registration.Enable(ExePath);
        registration.IsEnabled().ShouldBeTrue();

        registration.Disable();
        registration.IsEnabled().ShouldBeFalse();
    }

    [Fact]
    public void Desligar_sem_estar_ligado_nao_da_erro()
    {
        Should.NotThrow(() => Create().Disable());
    }

    [Fact]
    public void O_comando_tem_o_caminho_entre_aspas_e_pede_minimizado()
    {
        WindowsStartupRegistration.BuildCommand(ExePath)
            .ShouldBe("\"C:\\Program Files\\SunshineHelper\\SunshineHelper.exe\" --minimized");
    }

    [Fact]
    public void Grava_o_comando_no_registro()
    {
        Create().Enable(ExePath);

        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        key.ShouldNotBeNull();
        key.GetValue("SunshineHelperTest").ShouldBe(WindowsStartupRegistration.BuildCommand(ExePath));
    }

    [Fact]
    public void Corrige_o_caminho_quando_a_pasta_mudou()
    {
        var registration = Create();
        registration.Enable(@"C:\Old\SunshineHelper.exe");

        registration.RefreshIfStale(ExePath).ShouldBeTrue();

        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        key.ShouldNotBeNull();
        key.GetValue("SunshineHelperTest").ShouldBe(WindowsStartupRegistration.BuildCommand(ExePath));
    }

    [Fact]
    public void Nao_mexe_quando_o_caminho_ja_esta_certo()
    {
        var registration = Create();
        registration.Enable(ExePath);

        registration.RefreshIfStale(ExePath).ShouldBeFalse();
    }

    [Fact]
    public void Nunca_liga_sozinho()
    {
        var registration = Create();

        registration.RefreshIfStale(ExePath).ShouldBeFalse();
        registration.IsEnabled().ShouldBeFalse();
    }

    [Theory]
    [InlineData("--minimized", true)]
    [InlineData("--MINIMIZED", true)]
    [InlineData("--other", false)]
    public void Reconhece_o_argumento_de_minimizado(string argument, bool expected)
    {
        WindowsStartupRegistration.IsMinimizedLaunch(["--first", argument]).ShouldBe(expected);
    }

    [Fact]
    public void Sem_argumentos_nao_e_minimizado()
    {
        WindowsStartupRegistration.IsMinimizedLaunch([]).ShouldBeFalse();
    }
}
