using Shouldly;
using SunshineHelper.Infrastructure.Monitoring;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Monitoring;

public class SunshineProcessProbeTests
{
    [Fact]
    public void Lista_os_processos_sem_erro_e_sem_repetir()
    {
        var ids = new SunshineProcessProbe().FindProcessIds();

        ids.ShouldNotBeNull();
        ids.ShouldBeUnique();
    }

    [Fact]
    public void Procura_pelo_nome_do_executavel_do_Sunshine()
    {
        SunshineProcessProbe.ProcessName.ShouldBe("sunshine");
    }
}
