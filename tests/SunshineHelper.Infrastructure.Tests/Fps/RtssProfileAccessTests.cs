using Shouldly;
using SunshineHelper.Infrastructure.Fps;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Fps;

public sealed class RtssProfileAccessTests : IDisposable
{
    private const string Sid = "S-1-5-21-1000-2000-3000-1001";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sunshinehelper-tests-" + Guid.NewGuid().ToString("n"));

    public RtssProfileAccessTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void GlobalProfilePath_aponta_para_Profiles_Global()
    {
        RtssProfileAccess.GlobalProfilePath(@"C:\RTSS").ShouldBe(@"C:\RTSS\Profiles\Global");
    }

    [Fact]
    public void CanWrite_e_verdadeiro_para_um_arquivo_gravavel_e_nao_muda_o_conteudo()
    {
        string path = CreateProfile("conteudo");
        var before = File.GetLastWriteTimeUtc(path);

        RtssProfileAccess.CanWrite(path).ShouldBeTrue();

        File.ReadAllText(path).ShouldBe("conteudo");
        File.GetLastWriteTimeUtc(path).ShouldBe(before);
    }

    [Fact]
    public void CanWrite_e_falso_para_arquivo_somente_leitura()
    {
        string path = CreateProfile("x");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        RtssProfileAccess.CanWrite(path).ShouldBeFalse();
    }

    [Fact]
    public void CanWrite_e_falso_quando_o_arquivo_nao_existe()
    {
        RtssProfileAccess.CanWrite(Path.Combine(_directory, "nao-existe")).ShouldBeFalse();
    }

    [Fact]
    public void CanSaveGlobalProfile_olha_so_o_arquivo_quando_ele_existe()
    {
        string path = CreateProfile("x");
        RtssProfileAccess.CanSaveGlobalProfile(_directory).ShouldBeTrue();

        File.SetAttributes(path, FileAttributes.ReadOnly);
        RtssProfileAccess.CanSaveGlobalProfile(_directory).ShouldBeFalse();
    }

    [Fact]
    public void CanSaveGlobalProfile_sem_o_arquivo_testa_a_pasta_e_nao_deixa_lixo()
    {
        string profiles = Path.Combine(_directory, "Profiles");
        Directory.CreateDirectory(profiles);

        RtssProfileAccess.CanSaveGlobalProfile(_directory).ShouldBeTrue();
        Directory.GetFiles(profiles).ShouldBeEmpty();
    }

    [Fact]
    public void CanSaveGlobalProfile_e_falso_sem_a_pasta_Profiles()
    {
        RtssProfileAccess.CanSaveGlobalProfile(_directory).ShouldBeFalse();
    }

    [Fact]
    public void BuildIcaclsArguments_concede_so_leitura_e_escrita_no_arquivo_pelo_SID()
    {
        string path = @"C:\Program Files (x86)\RivaTuner Statistics Server\Profiles\Global";

        RtssProfileAccess.BuildIcaclsArguments(path, Sid)
            .ShouldBe("\"" + path + "\" /grant *" + Sid + ":(R,W)");
    }

    [Theory]
    [InlineData(@"Profiles\Global")]
    [InlineData(@"C:\RTSS\Profiles\Global.txt")]
    [InlineData(@"C:\RTSS\Global")]
    [InlineData(@"C:\RTSS\Profiles")]
    [InlineData("C:\\RTSS\\Profiles\\Global\" /grant *S-1-1-0:(F) \"C:\\x")]
    [InlineData("")]
    public void BuildIcaclsArguments_recusa_caminho_estranho(string path)
    {
        RtssProfileAccess.BuildIcaclsArguments(path, Sid).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Everyone")]
    [InlineData("S-1-5-21-1 /grant *S-1-1-0:(F)")]
    [InlineData("S-1-5-21-1\"")]
    public void BuildIcaclsArguments_recusa_SID_estranho(string sid)
    {
        RtssProfileAccess.BuildIcaclsArguments(@"C:\RTSS\Profiles\Global", sid).ShouldBeNull();
    }

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private string CreateProfile(string content)
    {
        string profiles = Path.Combine(_directory, "Profiles");
        Directory.CreateDirectory(profiles);
        string path = Path.Combine(profiles, "Global");
        File.WriteAllText(path, content);
        return path;
    }
}
