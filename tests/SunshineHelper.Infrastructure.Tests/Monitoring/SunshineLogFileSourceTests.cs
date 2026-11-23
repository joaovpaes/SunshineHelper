using System.Text;
using Shouldly;
using SunshineHelper.Infrastructure.Monitoring;
using Xunit;

namespace SunshineHelper.Infrastructure.Tests.Monitoring;

public class SunshineLogFileSourceTests
{
    private const int PollIntervalMs = 30;

    [Fact]
    public void Construtor_recusa_caminho_vazio()
    {
        Should.Throw<ArgumentException>(() => new SunshineLogFileSource(" ", PollIntervalMs));
    }

    [Fact]
    public void Construtor_recusa_intervalo_nao_positivo()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SunshineLogFileSource("qualquer.log", 0));
    }

    [Fact]
    public async Task ReadLinesAsync_ignora_conteudo_anterior_e_pega_so_linhas_novas()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string path = NewTempPath();
        try
        {
            await WriteAsync(path, "linha antiga 1\nlinha antiga 2\n", cancellationToken);

            var source = new SunshineLogFileSource(path, PollIntervalMs);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.ReadLinesAsync(cts.Token).GetAsyncEnumerator();

            // The source only reads its own "where does the log start" position once
            // MoveNextAsync actually runs — started here, before anything new is
            // written, so that baseline lands at the end of the *old* content.
            // Converted to a Task right away and never re-wrapped: a ValueTask may be
            // backed by a reusable, single-consumption source, so calling AsTask() more
            // than once on the same instance is not safe.
            Task<bool> pending = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(300, cancellationToken);

            await AppendAsync(path, "linha nova 1\n", cancellationToken);
            (await pending).ShouldBeTrue();
            enumerator.Current.ShouldBe("linha nova 1");

            await AppendAsync(path, "linha nova 2\n", cancellationToken);
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            enumerator.Current.ShouldBe("linha nova 2");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadLinesAsync_so_entrega_a_linha_depois_do_terminador()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string path = NewTempPath();
        try
        {
            await WriteAsync(path, string.Empty, cancellationToken);

            var source = new SunshineLogFileSource(path, PollIntervalMs);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.ReadLinesAsync(cts.Token).GetAsyncEnumerator();

            Task<bool> pending = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(300, cancellationToken); // baseline: empty file, position 0

            await AppendAsync(path, "parte inicial sem quebra", cancellationToken);

            // A few poll cycles go by with an unterminated line sitting in the file; it
            // must not be handed out as if it were complete.
            var finishedEarly = await Task.WhenAny(pending, Task.Delay(200, cancellationToken));
            finishedEarly.ShouldNotBe(pending, "a linha ainda não tem quebra de linha");

            await AppendAsync(path, " - resto da linha\n", cancellationToken);

            (await pending).ShouldBeTrue();
            enumerator.Current.ShouldBe("parte inicial sem quebra - resto da linha");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadLinesAsync_reinicia_do_zero_quando_o_arquivo_e_substituido_por_um_menor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string path = NewTempPath();
        try
        {
            await WriteAsync(path, "linha antiga bem mais longa que a próxima\n", cancellationToken);

            var source = new SunshineLogFileSource(path, PollIntervalMs);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.ReadLinesAsync(cts.Token).GetAsyncEnumerator();

            Task<bool> pending = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(300, cancellationToken); // baseline at the end of the old file

            // Simulates Sunshine restarting with a fresh, shorter log — the old position
            // no longer exists in the new file.
            await WriteAsync(path, "nova\n", cancellationToken);

            (await pending).ShouldBeTrue();
            enumerator.Current.ShouldBe("nova");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadLinesAsync_espera_o_arquivo_ser_criado_antes_de_ler()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string path = NewTempPath(); // deliberately never created before this point

        var source = new SunshineLogFileSource(path, PollIntervalMs);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var enumerator = source.ReadLinesAsync(cts.Token).GetAsyncEnumerator();

        Task<bool> pending = enumerator.MoveNextAsync().AsTask();

        // A few poll cycles with no file at all must not throw or complete.
        var finishedEarly = await Task.WhenAny(pending, Task.Delay(200, cancellationToken));
        finishedEarly.ShouldNotBe(pending);

        try
        {
            await WriteAsync(path, string.Empty, cancellationToken);
            await Task.Delay(300, cancellationToken); // baseline: now it exists, empty

            await AppendAsync(path, "primeira linha\n", cancellationToken);

            (await pending).ShouldBeTrue();
            enumerator.Current.ShouldBe("primeira linha");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"sunshinehelper-logtest-{Guid.NewGuid():n}.log");

    // Writes with the same FileShare.ReadWrite|Delete that SunshineLogFileSource itself
    // opens with, so these never race against its own open handle on share-mode alone —
    // Sunshine's real log writer does the same (it keeps the file open the whole session).
    // File.WriteAllTextAsync/AppendAllTextAsync are avoided here on purpose: their own
    // share mode is a BCL implementation detail this test shouldn't have to trust.
    private static async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task AppendAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
