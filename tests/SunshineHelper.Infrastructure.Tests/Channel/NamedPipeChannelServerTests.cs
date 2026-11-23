using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Shouldly;
using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Channel;
using SunshineHelper.Protocol;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Channel;

/// <summary>
/// Drives <see cref="NamedPipeChannelServer"/> through a real
/// <see cref="NamedPipeClientStream"/> — the thing a unit test with fakes on both ends
/// cannot catch: the actual line framing, the ACL not locking out the test's own
/// account, and a line that never ends.
/// </summary>
public sealed class NamedPipeChannelServerTests : IAsyncLifetime
{
    private TriggerChannelHandler? _handler;
    private NamedPipeChannelServer? _server;

    public ValueTask InitializeAsync()
    {
        var orchestrator = new SessionOrchestrator(
            modules: [],
            new FakeSessionSnapshotStore(),
            new FakeBootIdentityProvider());
        var settings = new FakeCurrentSettingsProvider(AppSettings.CreateDefault());
        _handler = new TriggerChannelHandler(orchestrator, settings);

        _server = new NamedPipeChannelServer(_handler);
        _server.Start();

        // No sleep needed to let the accept loop get going: ConnectAsync with a timeout
        // retries while the pipe does not exist yet, so the race is already covered.
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        _handler?.Dispose();
    }

    [Fact]
    public async Task Pedido_session_end_por_pipe_real_responde_nothing_to_do()
    {
        var response = await SendAsync(new TriggerRequest { Op = TriggerOp.SessionEnd });

        response.ShouldNotBeNull();
        response!.Ok.ShouldBeTrue();
        response.State.ShouldBe(ChannelState.NothingToDo);
    }

    [Fact]
    public async Task Linha_sem_fim_e_maior_que_o_limite_nao_derruba_o_servidor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // No trailing '\n', well past MaxLineBytes: the server has to give up on this
        // connection instead of buffering forever. It may do that by closing the pipe
        // while the client is still writing, so a broken write here is a pass too — what
        // must never happen is a reply to this, or a server that stops serving.
        try
        {
            using var client = await ConnectAsync();

            byte[] junk = Encoding.UTF8.GetBytes(new string('x', NamedPipeChannelServer.MaxLineBytes + 1024));
            await client.WriteAsync(junk, cancellationToken);
            await client.FlushAsync(cancellationToken);

            var readBuffer = new byte[1];
            int read = await client.ReadAsync(readBuffer, cancellationToken);
            read.ShouldBe(0, "o servidor não deveria responder a uma linha acima do limite");
        }
        catch (IOException)
        {
            // The server closed the pipe mid-write. Also correct.
        }

        // And it is still accepting perfectly good connections afterward.
        var response = await SendAsync(new TriggerRequest { Op = TriggerOp.SessionEnd });
        response!.Ok.ShouldBeTrue();
    }

    private static async Task<NamedPipeClientStream> ConnectAsync()
    {
        var client = new NamedPipeClientStream(".", ChannelPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5_000, TestContext.Current.CancellationToken);
        return client;
    }

    private static async Task<ChannelResponse?> SendAsync(TriggerRequest request)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await ConnectAsync();

        string json = JsonSerializer.Serialize(request, ProtocolJsonContext.Default.TriggerRequest);
        await client.WriteAsync(Encoding.UTF8.GetBytes(json + "\n"), cancellationToken);
        await client.FlushAsync(cancellationToken);

        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        string? line = await reader.ReadLineAsync(cancellationToken);
        return line is null ? null : JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.ChannelResponse);
    }
}
