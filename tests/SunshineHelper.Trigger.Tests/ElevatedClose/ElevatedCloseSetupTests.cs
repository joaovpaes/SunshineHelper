using System.Xml.Linq;
using Shouldly;
using SunshineHelper.Protocol;
using SunshineHelper.Trigger.ElevatedClose;
using Xunit;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

/// <summary>The pure parts of the install mode. Nothing here installs anything: on the CI runner the
/// tests run as administrator, and a real install would register the task on that machine.</summary>
public sealed class ElevatedCloseSetupTests
{
    private const string UserSid = "S-1-5-21-1000-2000-3000-1001";
    private static readonly XNamespace _task = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private static readonly byte[] _localSystem = [1, 1, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0];
    private static readonly byte[] _domainUser =
    [
        1, 5, 0, 0, 0, 0, 0, 5,
        21, 0, 0, 0,
        0xE8, 0x03, 0, 0,
        0xD0, 0x07, 0, 0,
        0xB8, 0x0B, 0, 0,
        0xE9, 0x03, 0, 0,
    ];

    private static readonly string[] _installTwice = ["install", "install"];
    private static readonly string[] _installUpper = ["Install"];
    private static readonly string[] _installAndMore = ["install", "--force"];

    [Fact]
    public void Caminhos_ficam_dentro_de_Program_Files()
    {
        var paths = ElevatedCloseInstallPaths.Under(@"C:\Program Files");

        paths.Folder.ShouldBe(@"C:\Program Files\SunshineHelper");
        paths.Executable.ShouldBe(@"C:\Program Files\SunshineHelper\SunshineHelper.Trigger.exe");
    }

    [Fact]
    public void Tarefa_roda_como_o_usuario_com_o_maximo_de_privilegio_so_quando_ele_esta_conectado()
    {
        XElement principal = Definition().Descendants(_task + "Principal").Single();

        principal.Element(_task + "UserId")!.Value.ShouldBe(UserSid);
        principal.Element(_task + "LogonType")!.Value.ShouldBe("InteractiveToken");
        principal.Element(_task + "RunLevel")!.Value.ShouldBe("HighestAvailable");
    }

    [Fact]
    public void Tarefa_so_roda_o_auxiliar_instalado_com_elevated_close_na_frente()
    {
        XElement exec = Definition().Descendants(_task + "Exec").Single();

        exec.Element(_task + "Command")!.Value.ShouldBe("\"C:\\Program Files\\SunshineHelper\\SunshineHelper.Trigger.exe\"");
        exec.Element(_task + "Arguments")!.Value.ShouldBe("elevated-close $(Arg0) $(Arg1) $(Arg2)");
        exec.Element(_task + "WorkingDirectory")!.Value.ShouldBe(@"C:\Program Files\SunshineHelper");
        Definition().Descendants(_task + "Exec").Count().ShouldBe(1);
    }

    [Fact]
    public void Tarefa_sem_gatilho_oculta_curta_e_uma_por_vez()
    {
        XDocument definition = Definition();
        XElement settings = definition.Descendants(_task + "Settings").Single();

        definition.Descendants(_task + "Triggers").ShouldBeEmpty();
        settings.Element(_task + "Hidden")!.Value.ShouldBe("true");
        settings.Element(_task + "ExecutionTimeLimit")!.Value.ShouldBe("PT30S");
        settings.Element(_task + "MultipleInstancesPolicy")!.Value.ShouldBe("IgnoreNew");
        settings.Element(_task + "AllowStartOnDemand")!.Value.ShouldBe("true");
        definition.Descendants(_task + "URI").Single().Value.ShouldBe("\\" + ElevatedCloseContract.TaskName);
    }

    [Fact]
    public void Caracteres_especiais_no_caminho_nao_quebram_o_XML()
    {
        var paths = ElevatedCloseInstallPaths.Under(@"C:\Arquivos & Programas <x>");

        XDocument definition = XDocument.Parse(ElevatedCloseTaskDefinition.Build(UserSid, paths));

        definition.Descendants(_task + "WorkingDirectory").Single().Value.ShouldBe(@"C:\Arquivos & Programas <x>\SunshineHelper");
    }

    [Fact]
    public void SID_do_sistema_vira_texto()
    {
        SidText.Format(_localSystem).ShouldBe("S-1-5-18");
    }

    [Fact]
    public void SID_de_usuario_vira_texto()
    {
        SidText.Format(_domainUser).ShouldBe("S-1-5-21-1000-2000-3000-1001");
    }

    [Fact]
    public void Bytes_que_nao_sao_SID_dao_nulo()
    {
        SidText.Format([1, 2, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0]).ShouldBeNull();
        SidText.Format([2, 1, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0]).ShouldBeNull();
        SidText.Format([1, 1, 0]).ShouldBeNull();
    }

    [Theory]
    [InlineData("install", true)]
    [InlineData("uninstall", true)]
    [InlineData("Install", false)]
    [InlineData("elevated-close", false)]
    [InlineData("start", false)]
    public void So_install_e_uninstall_escolhem_o_modo(string verb, bool expected)
    {
        ElevatedCloseSetupMode.IsSetupVerb(verb).ShouldBe(expected);
    }

    [Fact]
    public void Argumentos_a_mais_ou_diferentes_sao_recusados_antes_de_qualquer_coisa()
    {
        ElevatedCloseSetupMode.Run(_installTwice).ShouldBe((int)ElevatedCloseSetupExitCode.BadArguments);
        ElevatedCloseSetupMode.Run(_installUpper).ShouldBe((int)ElevatedCloseSetupExitCode.BadArguments);
        ElevatedCloseSetupMode.Run(_installAndMore).ShouldBe((int)ElevatedCloseSetupExitCode.BadArguments);
        ElevatedCloseSetupMode.Run([]).ShouldBe((int)ElevatedCloseSetupExitCode.BadArguments);
    }

    private static XDocument Definition() =>
        XDocument.Parse(ElevatedCloseTaskDefinition.Build(UserSid, ElevatedCloseInstallPaths.Under(@"C:\Program Files")));
}
