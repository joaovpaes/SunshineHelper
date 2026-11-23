using SunshineHelper.Application.Channel;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Channel;
using SunshineHelper.Protocol;

// Hosts the real channel (NamedPipeChannelServer + TriggerChannelHandler +
// SessionOrchestrator) so SunshineHelper.Trigger.exe can be pointed at it by hand.
// Covers docs/testes-manuais.md, "PR 7 - Auxiliar de gatilho e canal".
//
// The only profile here leaves every Display value alone (ProfileValueMode.Leave), so
// nothing on screen actually changes — this harness is about the pipe and the protocol,
// not the modules, which PRs 5a/5b/6 already have their own manual checklists for.

const string ProfileId = "perfil-de-teste";

var settings = new AppSettings
{
    SchemaVersion = SettingsSchema.CurrentVersion,
    DefaultProfileId = ProfileId,
    Profiles =
    [
        new ClientProfile
        {
            Id = ProfileId,
            Name = "Perfil de teste (não muda nada na tela)",
            Display = new DisplayProfileSettings
            {
                Resolution = new ProfileValue<Resolution> { Mode = ProfileValueMode.Leave },
                Hdr = new ProfileValue<bool> { Mode = ProfileValueMode.Leave },
                FpsLimit = new ProfileValue<int> { Mode = ProfileValueMode.Leave },
            },
        },
    ],
};

var orchestrator = new SessionOrchestrator(
    modules: [],
    new InMemorySessionSnapshotStore(),
    new AlwaysUnknownBootIdentityProvider());

var handler = new TriggerChannelHandler(orchestrator, new StaticSettingsProvider(settings));

await using var server = new NamedPipeChannelServer(handler);
server.Start();

Console.WriteLine($"Canal escutando em \\\\.\\pipe\\{ChannelPipe.Name}.");
Console.WriteLine();
Console.WriteLine("Em outro terminal (pode ser o mesmo usuário ou um administrador — a");
Console.WriteLine("ACL deve deixar os dois passarem, nunca outra conta), publique e rode");
Console.WriteLine("o auxiliar de verdade:");
Console.WriteLine();
Console.WriteLine("  dotnet run --project src\\SunshineHelper.Trigger -- start");
Console.WriteLine("  dotnet run --project src\\SunshineHelper.Trigger -- end");
Console.WriteLine();
Console.WriteLine("Confira:");
Console.WriteLine("  - os dois comandos sempre terminam com código de saída 0");
Console.WriteLine("    (echo %ERRORLEVEL% depois, no cmd, ou $LASTEXITCODE no PowerShell);");
Console.WriteLine("  - 'start' sem SUNSHINE_CLIENT_NAME definido ainda funciona (cai no");
Console.WriteLine("    perfil padrão acima);");
Console.WriteLine("  - 'end' sem um 'start' antes responde sem erro (nada para reverter);");
Console.WriteLine("  - rodando um terminal como administrador e outro sem, os dois conectam.");
Console.WriteLine();
Console.WriteLine("Pressione Enter para encerrar o canal.");
Console.ReadLine();

internal sealed class InMemorySessionSnapshotStore : ISessionSnapshotStore
{
    private string? _content;

    public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(_content);

    public Task WriteAtomicAsync(string content, CancellationToken cancellationToken)
    {
        _content = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        _content = null;
        return Task.CompletedTask;
    }

    public bool Exists() => _content is not null;
}

internal sealed class AlwaysUnknownBootIdentityProvider : IBootIdentityProvider
{
    public BootIdentity? GetCurrent() => BootIdentity.Unknown;
}

internal sealed class StaticSettingsProvider(AppSettings settings) : ICurrentSettingsProvider
{
    public AppSettings Current { get; } = settings;
}
