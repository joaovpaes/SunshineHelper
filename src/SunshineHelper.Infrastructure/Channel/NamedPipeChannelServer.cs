using System.IO.Pipes;
using System.IO.Pipes.AccessControl;
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
/// <b>ACL:</b> restricted to the interactive user, <c>SYSTEM</c> and
/// <c>Administrators</c>, never any specific account — Sunshine may run as a service,
/// and nothing about the author's own PC belongs in this code (CLAUDE.md). A machine
/// with other local accounts must not be able to reach this pipe at all.
/// </para>
/// <para>
/// <b>Bounded reads:</b> <see cref="StreamReader.ReadLineAsync()"/> has no length cap,
/// so a line with no terminator from a confused or hostile local process could grow
/// without bound. <see cref="ReadBoundedLineAsync"/> reads byte by byte up to
/// <see cref="MaxLineBytes"/> and gives up past it instead.
/// </para>
/// <para>
/// <b>One connection at a time, many instances:</b> each accepted connection gets its
/// own <see cref="NamedPipeServerStream"/> and its own handler task, so the auxiliary
/// never has to queue behind another one just to open the pipe — the actual
/// serialization against the orchestrator happens inside
/// <see cref="TriggerChannelHandler"/>, not here.
/// </para>
/// </remarks>
public sealed class NamedPipeChannelServer : IAsyncDisposable
{
    /// <summary>Generous for a JSON line describing one client (docs/fase0-resultados.md's
    /// <c>SUNSHINE_CLIENT_*</c> variables), tiny next to what a runaway write could
    /// otherwise reach.</summary>
    public const int MaxLineBytes = 64 * 1024;

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
            NamedPipeServerStream pipe = CreateServerStream();
            try
            {
                await pipe.WaitForConnectionAsync(serverLifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                // A client went away before the handshake finished. Not this loop's
                // problem — wait for the next one.
                await pipe.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            _ = HandleConnectionAsync(pipe, serverLifetime);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken serverLifetime)
    {
        await using (pipe)
        {
            try
            {
                string? line = await ReadBoundedLineAsync(pipe, serverLifetime).ConfigureAwait(false);
                if (line is not { Length: > 0 })
                {
                    return;
                }

                var request = JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.TriggerRequest);
                if (request is null)
                {
                    return;
                }

                // serverLifetime, not a per-connection token: the auxiliary going away
                // must never abort the real apply/revert (see TriggerChannelHandler's
                // own remarks on this exact point).
                ChannelResponse response = await _handler.HandleAsync(request, serverLifetime).ConfigureAwait(false);

                string json = JsonSerializer.Serialize(response, ProtocolJsonContext.Default.ChannelResponse);
                byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
                await pipe.WriteAsync(bytes, serverLifetime).ConfigureAwait(false);
                await pipe.FlushAsync(serverLifetime).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // Malformed request: nothing sensible to reply with, and the auxiliary
                // always exits 0 regardless (its own rule, not this server's to enforce).
            }
            catch (IOException)
            {
                // The auxiliary disconnected mid-write, most likely because it hit its
                // own barrier first. TriggerChannelHandler.HandleAsync had already been
                // given serverLifetime, so whatever it started keeps running.
            }
            catch (OperationCanceledException)
            {
                // Server shutting down.
            }
        }
    }

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

    private static NamedPipeServerStream CreateServerStream()
    {
        var security = new PipeSecurity();

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

        return NamedPipeServerStreamAcl.Create(
            ChannelPipe.Name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }

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
