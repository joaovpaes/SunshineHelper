using Shouldly;
using SunshineHelper.Infrastructure.Display;
using SunshineHelper.Infrastructure.Fps;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Display;

/// <summary>Guards the order decided in docs/adr/0010-hdr-antes-da-resolucao.md: an HDR
/// switch resets the display mode, so HDR must be applied before the resolution (and
/// therefore reverted after it).</summary>
public class ModuleOrderTests
{
    [Fact]
    public void Hdr_vem_antes_da_resolucao()
    {
        new HdrModule().Order.ShouldBeLessThan(new PrimaryDisplayModule().Order);
    }

    [Fact]
    public void Fps_vem_depois_de_hdr_e_resolucao()
    {
        int fps = new RtssFpsLimitModule().Order;

        fps.ShouldBeGreaterThan(new HdrModule().Order);
        fps.ShouldBeGreaterThan(new PrimaryDisplayModule().Order);
    }
}
