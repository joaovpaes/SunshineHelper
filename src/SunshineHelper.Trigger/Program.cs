using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SunshineHelper.Protocol;

// SunshineHelper.Trigger: called by Sunshine as a Prep Command (Do/Undo).
// Rule that must never break (docs/adr/0002-arquitetura-do-nucleo.md):
// this process always exits 0. A non-zero exit aborts the Sunshine stream.
//
// args[0] is "start" for the Do command, "end" for the Undo command — Sunshine only
// ever runs Undo on an explicit /cancel (docs/fase0-resultados.md); a plain disconnect
// never calls this process at all, and is handled by the log reader instead
// (docs/fase1-plano.md, PR 8), in-process, with no pipe involved.
//
// Whatever happens below — the app isn't running, the pipe times out, the reply is
// garbage — this process still exits 0. There is no case where holding up or failing
// the Sunshine stream is the right outcome for a helper auxiliary.

// This process has no settings.json of its own to read, so it mirrors
// TimingSettings' own defaults here rather than reaching across processes for them —
// the app is the one that actually enforces the real budget once the request arrives
// (docs/adr/0002-arquitetura-do-nucleo.md, "Orçamento de tempo").
const int DefaultWaitMs = 20_000;
const int AbsoluteCeilingMs = 55_000;
const int ConnectGraceMs = 2_000;

try
{
    await RunAsync(args).ConfigureAwait(false);
}
catch (Exception)
{
    // See the rule above: nothing to do here but let the stream proceed unconfigured.
}

return 0;

static async Task RunAsync(string[] args)
{
    if (args.Length == 0)
    {
        return;
    }

    TriggerRequest? request = BuildRequest(args[0]);
    if (request is null)
    {
        return;
    }

    using var client = new NamedPipeClientStream(".", ChannelPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);

    // Connecting and the whole round trip share one hard ceiling
    // (docs/adr/0002-arquitetura-do-nucleo.md, "Orçamento de tempo", teto absoluto):
    // even if the app is up but something about the reply itself hangs, this process
    // must still give up well before the client Sunshine is serving gives up on it.
    int overallMs = Math.Min(request.WaitMs!.Value + ConnectGraceMs, AbsoluteCeilingMs);
    using var overall = new CancellationTokenSource(TimeSpan.FromMilliseconds(overallMs));

    try
    {
        await client.ConnectAsync(overall.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // The app isn't up to answer. The ADR calls for trying to start it once here
        // and waiting again, through the scheduled task the installer PR sets up
        // (docs/fase1-plano.md) — until that PR lands, a missing app just means the
        // stream starts (or ends) with the environment left exactly as it is.
        return;
    }

    string json = JsonSerializer.Serialize(request, ProtocolJsonContext.Default.TriggerRequest);
    byte[] requestBytes = Encoding.UTF8.GetBytes(json + "\n");
    await client.WriteAsync(requestBytes, overall.Token).ConfigureAwait(false);
    await client.FlushAsync(overall.Token).ConfigureAwait(false);

    // The reply is read but deliberately not acted on: whatever it says — applied,
    // timeout, even a failure — this process still exits 0 either way. Reading it here
    // is only so the connection closes cleanly instead of being torn down mid-write on
    // the app's side; a future PR may log it for diagnostics.
    using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
    await reader.ReadLineAsync(overall.Token).ConfigureAwait(false);
}

static TriggerRequest? BuildRequest(string op) => op switch
{
    "start" => new TriggerRequest
    {
        Op = TriggerOp.SessionStart,
        Client = ReadClientFromEnvironment(),
        WaitMs = DefaultWaitMs,
    },
    "end" => new TriggerRequest
    {
        Op = TriggerOp.SessionEnd,
        WaitMs = DefaultWaitMs,
    },
    _ => null,
};

// Sunshine passes these to every Prep Command (docs/fase0-resultados.md).
static ClientInfo ReadClientFromEnvironment() => new()
{
    Name = Environment.GetEnvironmentVariable("SUNSHINE_CLIENT_NAME"),
    Width = ParseInt(Environment.GetEnvironmentVariable("SUNSHINE_CLIENT_WIDTH")),
    Height = ParseInt(Environment.GetEnvironmentVariable("SUNSHINE_CLIENT_HEIGHT")),
    Fps = ParseInt(Environment.GetEnvironmentVariable("SUNSHINE_CLIENT_FPS")),
    Hdr = ParseBool(Environment.GetEnvironmentVariable("SUNSHINE_CLIENT_HDR")),
};

static int? ParseInt(string? value) =>
    int.TryParse(value, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

static bool? ParseBool(string? value) => value switch
{
    null => null,
    "1" => true,
    "0" => false,
    _ => bool.TryParse(value, out bool parsed) ? parsed : null,
};
