using Shouldly;
using SunshineHelper.Domain.Monitoring;
using SunshineHelper.Domain.Tests.TestSupport;
using Xunit;

namespace SunshineHelper.Domain.Tests.Monitoring;

/// <summary>
/// Runs the parser over the real, redacted logs in tests/fixtures/sunshine — the plan's
/// own requirement for this PR ("testes com as fixtures da Fase 0",
/// docs/fase1-plano.md) — instead of hand-copied line fragments, so a change to those
/// fixtures is caught here too.
/// </summary>
public class SunshineLogLineParserFixtureTests
{
    [Fact]
    public void Celular_desconecta_resume_e_depois_cancela()
    {
        string[] lines = FixtureFiles.ReadSunshineLog("phone-disconnect-resume-cancel.log")
            .Split('\n', StringSplitOptions.TrimEntries);

        var events = ParseAll(lines);

        SunshineSessionEvent[] expected =
        [
            SunshineSessionEvent.ClientConnected,
            SunshineSessionEvent.ClientDisconnected,
            SunshineSessionEvent.SessionEnded,
            SunshineSessionEvent.Resumed,
            SunshineSessionEvent.ClientConnected,
            SunshineSessionEvent.ClientDisconnected,
            SunshineSessionEvent.SessionEnded,
            SunshineSessionEvent.CancelRequested,
        ];
        events.ShouldBe(expected);
    }

    [Fact]
    public void TV_conecta_e_cancela_sem_desconectar_antes()
    {
        string[] lines = FixtureFiles.ReadSunshineLog("tv-hdr-session.log")
            .Split('\n', StringSplitOptions.TrimEntries);

        var events = ParseAll(lines);

        // The TV fixture's scenario (tests/fixtures/sunshine/README.md): /cancel with no
        // CLIENT DISCONNECTED before it — the detail the monitor's state machine uses to
        // tell an explicit cancel apart from an ordinary disconnect.
        SunshineSessionEvent[] expected =
        [
            SunshineSessionEvent.ClientConnected,
            SunshineSessionEvent.CancelRequested,
            SunshineSessionEvent.SessionEnded,
        ];
        events.ShouldBe(expected);
    }

    private static List<SunshineSessionEvent> ParseAll(IEnumerable<string> lines) =>
        lines
            .Select(SunshineLogLineParser.TryParse)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .ToList();
}
