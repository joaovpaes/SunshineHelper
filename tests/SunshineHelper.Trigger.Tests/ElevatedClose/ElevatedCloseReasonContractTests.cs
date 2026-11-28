using Shouldly;
using SunshineHelper.Application.CloseGame;
using SunshineHelper.Trigger.ElevatedClose;
using Xunit;

namespace SunshineHelper.Trigger.Tests.ElevatedClose;

/// <summary>The auxiliary and the app keep separate copies of the <c>elevated-close</c> exit codes
/// (the auxiliary references neither Application nor Infrastructure). These tests keep them equal,
/// so the log never shows the wrong reason.</summary>
public sealed class ElevatedCloseReasonContractTests
{
    [Fact]
    public void Cada_codigo_do_auxiliar_existe_no_app_com_o_mesmo_nome()
    {
        foreach (ElevatedCloseExitCode code in Enum.GetValues<ElevatedCloseExitCode>())
        {
            ((ElevatedCloseReason)(int)code).ToString().ShouldBe(code.ToString());
        }
    }

    [Fact]
    public void O_app_nao_tem_codigos_que_o_auxiliar_nao_usa()
    {
        foreach (ElevatedCloseReason reason in Enum.GetValues<ElevatedCloseReason>())
        {
            ((ElevatedCloseExitCode)(int)reason).ToString().ShouldBe(reason.ToString());
        }
    }
}
