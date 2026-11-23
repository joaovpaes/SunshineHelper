namespace SunshineHelper.Domain.Monitoring;

/// <summary>
/// Recognizes the handful of Sunshine log lines the session monitor reacts to
/// (docs/adr/0004-leitor-do-log.md, tests/fixtures/sunshine). Matches on the fixed
/// substrings Sunshine itself writes, never on the timestamp or log level that precede
/// them, so a change to the timestamp format or to Debug/Info labeling would not break
/// detection.
/// </summary>
public static class SunshineLogLineParser
{
    private const string ClientConnectedMarker = "CLIENT CONNECTED";
    private const string ClientDisconnectedMarker = "CLIENT DISCONNECTED";
    private const string CancelMarker = "DESTINATION :: /cancel";
    private const string ResumeMarker = "DESTINATION :: /resume";
    private const string SessionEndedMarker = "Session ended";

    /// <summary>Null when the line carries no event this app reacts to — true of the
    /// overwhelming majority of Sunshine's log, including every line of a healthy,
    /// uneventful stream.</summary>
    public static SunshineSessionEvent? TryParse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        // CLIENT DISCONNECTED contains CLIENT CONNECTED's text nowhere, and vice versa,
        // so the order between these two does not matter. /cancel and /resume are
        // likewise mutually exclusive substrings of the same line. Checked separately
        // from "Session ended" because that marker can follow either one.
        if (line.Contains(ClientConnectedMarker, StringComparison.Ordinal))
        {
            return SunshineSessionEvent.ClientConnected;
        }

        if (line.Contains(ClientDisconnectedMarker, StringComparison.Ordinal))
        {
            return SunshineSessionEvent.ClientDisconnected;
        }

        if (line.Contains(CancelMarker, StringComparison.Ordinal))
        {
            return SunshineSessionEvent.CancelRequested;
        }

        if (line.Contains(ResumeMarker, StringComparison.Ordinal))
        {
            return SunshineSessionEvent.Resumed;
        }

        if (line.Contains(SessionEndedMarker, StringComparison.Ordinal))
        {
            return SunshineSessionEvent.SessionEnded;
        }

        return null;
    }
}
