using Shouldly;
using SunshineHelper.Domain.Monitoring;
using Xunit;

namespace SunshineHelper.Domain.Tests.Monitoring;

public class SunshineLogLineParserTests
{
    [Theory]
    [InlineData("[2026-11-22 09:09:22.645]: Info: CLIENT CONNECTED", SunshineSessionEvent.ClientConnected)]
    [InlineData("[2026-11-22 09:09:39.998]: Info: CLIENT DISCONNECTED", SunshineSessionEvent.ClientDisconnected)]
    [InlineData("[2026-11-22 09:10:03.372]: Debug: DESTINATION :: /cancel", SunshineSessionEvent.CancelRequested)]
    [InlineData("[2026-11-22 09:09:48.354]: Debug: DESTINATION :: /resume", SunshineSessionEvent.Resumed)]
    [InlineData("[2026-11-22 09:09:40.350]: Debug: Session ended", SunshineSessionEvent.SessionEnded)]
    public void TryParse_reconhece_cada_marcador(string line, SunshineSessionEvent expected)
    {
        SunshineLogLineParser.TryParse(line).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[2026-11-22 09:08:54.788]: Debug: DESTINATION :: /serverinfo")]
    [InlineData("[2026-11-22 09:08:54.788]: Debug: uuid -- <REDACTED>")]
    [InlineData("[2026-11-22 09:09:21.312]: Info: Executing: [C:\\Playnite\\Playnite.FullscreenApp.exe]")]
    [InlineData("[2026-11-22 09:10:03.372]: Info: Executing Undo Cmd: [\"RTSS_OFF.bat\"]")]
    [InlineData("")]
    public void TryParse_ignora_linhas_sem_marcador(string line)
    {
        SunshineLogLineParser.TryParse(line).ShouldBeNull();
    }

    [Fact]
    public void TryParse_lanca_para_linha_nula()
    {
        Should.Throw<ArgumentNullException>(() => SunshineLogLineParser.TryParse(null!));
    }

    [Fact]
    public void TryParse_destino_cancel_nao_e_confundido_com_resume()
    {
        // The two markers share the prefix "DESTINATION :: /" but nothing past the
        // slash, so a substring match on one alone is enough to tell them apart.
        SunshineLogLineParser.TryParse("Debug: DESTINATION :: /cancel").ShouldBe(SunshineSessionEvent.CancelRequested);
        SunshineLogLineParser.TryParse("Debug: DESTINATION :: /resume").ShouldBe(SunshineSessionEvent.Resumed);
    }
}
