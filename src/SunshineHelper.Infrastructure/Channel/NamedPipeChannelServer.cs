using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using SunshineHelper.Application.Channel;
using SunshineHelper.Protocol;

namespace SunshineHelper.Infrastructure.Channel;

/// <summary>
/// The pipe side of docs/adr/0002-arquitetura-do-nucleo.md's "Canal entre o auxiliar e
/// o app": listens on <see cref="ChannelPipe.Name"/>, reads one JSON line per
/// connection, hands the parsed <see cref="TriggerRequest"/> to
/// <see cref="TriggerChannelHandler"/>, and writes back one JSON line with the
/// <see cref="ChannelResponse"/>. Everything about matching a profile or calling the
/// orchestrator belongs to the handler; this class is pure pipe plumbing, which is also
/// why it has no unit tests of its own — see
/// tests/SunshineHelper.Infrastructure.Tests for the real client+server coverage.
/// </summary>
/// <remarks>
/// <para>
/// <b>ACL:</b> restricted to the account running the app, the interactive user,
/// <c>SYSTEM</c> and <c>Administrators</c> — never any specific account, because nothing
/// about the author's own PC belongs in this code (CLAUDE.md). Sunshine may run as a
/// service, hence <c>SYSTEM</c>; the app may be elevated, hence <c>Administrators</c>;
/// and the running account is listed explicitly because an unelevated administrator's
/// token carries the <c>Administrators</c> SID as deny-only, so that ACE alone would not
/// let the app reach its own pipe. A machine with other local accounts cannot reach it.
/// </para>
/// <para>
/// <b>Bounded reads:</b> <see cref="StreamReader.ReadLineAsync()"/> has no length cap,
/// so a line with no terminator from a confused or hostile local process could grow
/// without bound. <see cref="ReadBoundedLineAsync"/> reads up to
/// <see cref="MaxLineBytes"/> and gives up past that. Each connection also gets
/// <see cref="IoTimeout"/> to send its line and to take the reply, so a process that
/// connects and then says nothing cannot sit on the channel either.
/// </para>
/// <para>
/// <b>One task per connection:</b> the accept loop goes straight back to listening as
/// soon as a client connects, so the pipe is never busy while an earlier connection is
/// still waiting out its <c>waitMs</c>. That matters: the auxiliary gives up on
/// connecting in about two seconds (it cannot stall the stream start longer than that),
/// which it could never do if a pending reply blocked the pipe. Serializing the real
/// work is <see cref="TriggerChannelHandler"/>'s job, not this loop's.
/// </para>
/// </remarks>
public sealed class NamedPipeChannelServer : IAsyncDisposable
{
    /// <summary>Generous for a JSON line describing one client (docs/fase0-resultados.md's
    /// <c>SUNSHINE_CLIENT_*</c> variables), tiny next to what a runaway write could
    /// otherwise reach.</summary>
    public const int MaxLineBytes = 64 * 1024;

    /// <summary>How long one connection gets to send its request line, and separately to
    /// take its reply. Both are immediate in practice — the long wait happens between
    /// them, inside the handler, and is bounded by its own budget.</summary>
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Pause before rebuilding the pipe after a failure to create it at all, so
    /// a lasting cause does not turn the accept loop into a busy loop.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly TriggerChannelHandler _handler;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _acceptLoop;

    public NamedPipeChannelServer(TriggerChannelHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Starts accepting connections in the background. Call once; disposing
    /// the server is how it stops.</summary>
    public void Start()
    {
        _acceptLoop = AcceptLoopAsync(_stopping.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken serverLifetime)
    {
        while (!serverLifetime.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = CreateServerStream();
            }
            catch (Exception)
            {
                // The pipe could not be created at all — another process holding the
                // name, or the OS refusing. Worth retrying, but not at full speed.
                if (!await DelayAsync(RetryDelay, serverLifetime).ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            try
            {
                await pipe.WaitForConnectionAsync(serverLifetime).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Either the server is shutting down, or a client vanished mid-handshake.
                await pipe.DisposeAsync().ConfigureAwait(false);

                if (serverLifetime.IsCancellationRequested)
                {
                    return;
                }

                continue;
            }

            // Deliberately not awaited: this connection finishes on its own while the
            // loop goes back to listening (see the remarks above). ServeAsync owns the
            // stream from here, disposes it, and never throws.
            _ = ServeAsync(pipe, serverLifetime);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken serverLifetime)
    {
        try
        {
            TriggerRequest? request = await ReadRequestAsync(pipe, serverLifetime).ConfigureAwait(false);
            if (request is not null)
            {
                // serverLifetime, never a per-connection token: the auxiliary going away
                // must not abort the real apply/revert (see TriggerChannelHandler's own
                // remarks on this exact point).
                ChannelResponse response = await _handler
                    .HandleAsync(request, serverLifetime)
                    .ConfigureAwait(false);

                await WriteResponseAsync(pipe, response, serverLifetime).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // This task is deliberately unobserved, so it must not throw. There is
            // nothing left to reply with by this point, and the auxiliary exits 0 either
            // way; whatever the handler already started is the orchestrator's business,
            // not this connection's.
        }
        finally
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Null for anything that is not a usable request: no line, a line past the
    /// cap, malformed JSON, a client that connected and said nothing, or a disconnect.
    /// None of those deserve a reply — the auxiliary exits 0 regardless.</summary>
    private static async Task<TriggerRequest?> ReadRequestAsync(Stream stream, CancellationToken serverLifetime)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(serverLifetime);
        deadline.CancelAfter(IoTimeout);

        string? line;
        try
        {
            line = await ReadBoundedLineAsync(stream, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception) when (!serverLifetime.IsCancellationRequested)
        {
            // Silent client, oversized line, or broken pipe.
            return null;
        }

        if (line is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.TriggerRequest);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        ChannelResponse response,
        CancellationToken serverLifetime)
    {
        string json = JsonSerializer.Serialize(response, ProtocolJsonContext.Default.ChannelResponse);
        byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(serverLifetime);
        deadline.CancelAfter(IoTimeout);

        try
        {
            await stream.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
            await stream.FlushAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception) when (!serverLifetime.IsCancellationRequested)
        {
            // The auxiliary hit its own barrier and closed the pipe, or stopped reading.
            // Expected, and harmless: the real work was handed serverLifetime, not this.
        }
    }

    /// <summary>One byte at a time, which is plenty for a request of a few hundred bytes
    /// twice per streaming session, and keeps the cap obviously correct.</summary>
    private static async Task<string?> ReadBoundedLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var oneByte = new byte[1];

        while (true)
        {
            int read = await stream.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                // Connection closed. Whatever arrived without a trailing newline is
                // still worth trying to parse — the auxiliary always writes one, but a
                // test client or a future version might not.
                return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (oneByte[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (buffer.Length >= MaxLineBytes)
            {
                throw new InvalidDataException(
                    $"Linha do canal maior que o limite de {MaxLineBytes} bytes.");
            }

            buffer.WriteByte(oneByte[0]);
        }
    }

    private static NamedPipeServerStream CreateServerStream() => NamedPipeServerStreamAcl.Create(
        ChannelPipe.Name,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous,
        inBufferSize: 0,
        outBufferSize: 0,
        pipeSecurity: CreatePipeSecurity());

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();

        // The account this app is running as. Listed explicitly because an unelevated
        // administrator's token carries the Administrators SID as deny-only, which would
        // leave the app unable to use its own pipe through the ACE below.
        using var current = WindowsIdentity.GetCurrent();
        if (current.User is { } runningAs)
        {
            security.AddAccessRule(new PipeAccessRule(
                runningAs,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        // Interactive user: whoever is at the desktop Sunshine is streaming from.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, domainSid: null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // SYSTEM: Sunshine itself may run as a service, in which case the auxiliary it
        // launches runs as SYSTEM too.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Administrators: an elevated SunshineHelper.exe, or a manual test run from an
        // elevated prompt (docs/testes-manuais.md).
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }

    /// <summary>False when the server is shutting down.</summary>
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken serverLifetime)
    {
        try
        {
            await Task.Delay(delay, serverLifetime).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Stops listening. Connections already being served notice the cancelled
    /// token and finish on their own; they are not waited for, because one of them may
    /// legitimately still be applying a profile.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: that is exactly what cancelling the accept loop causes.
            }
        }

        _stopping.Dispose();
    }
}
