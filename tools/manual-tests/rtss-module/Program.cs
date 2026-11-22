using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Infrastructure.Fps;

// Ferramenta de teste manual do RtssFpsLimitModule — não faz parte do app, não é instalada
// e não entra no CI (sem RTSS instalado no runner do GitHub). Cobre o checklist de
// docs/testes-manuais.md, "PR 6 — Módulo de FPS (RTSS)".
//
// Rodar (PowerShell comum, não precisa ser administrador, fora de uma transmissão):
//   cd tools/manual-tests/rtss-module
//   dotnet run
//
// Ou, da raiz do repositório:
//   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\rtss-module\run.ps1
//
// Se o RTSS não estiver instalado nesta máquina, as opções 1 e 2 devem dizer isso
// claramente (em vez de travar ou lançar uma exceção), e a opção 3 deve falhar com uma
// mensagem explicando que não há RTSS para aplicar o limite — é assim que o módulo se
// comporta de propósito (ver o <remarks> da classe).

var module = new RtssFpsLimitModule();
ModuleSnapshot? captured = null;

while (true)
{
    PrintMenu(captured);
    string? choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await ShowCurrentStateAsync(module).ConfigureAwait(false);
            break;

        case "2":
            captured = await CaptureAsync(module).ConfigureAwait(false);
            break;

        case "3":
            await ApplyAsync(module, captured).ConfigureAwait(false);
            break;

        case "4":
            await ApplyNoLimitAsync(module, captured).ConfigureAwait(false);
            break;

        case "5":
            await RevertAsync(module, captured).ConfigureAwait(false);
            break;

        case "0":
            return;

        default:
            Console.WriteLine("Opção inválida.");
            break;
    }
}

static void PrintMenu(ModuleSnapshot? captured)
{
    Console.WriteLine();
    Console.WriteLine("=== Teste manual: módulo de FPS (RTSS) ===");
    Console.WriteLine(captured is null
        ? "Nada capturado ainda — comece pela opção 2."
        : $"Capturado: {Describe(captured)} (é para onde a opção 5 deve devolver)");
    Console.WriteLine("1) Mostrar o estado atual (instalado? limite de FPS?)");
    Console.WriteLine("2) Capturar o estado atual, antes de aplicar qualquer coisa");
    Console.WriteLine("3) Aplicar um limite de FPS (pede o número)");
    Console.WriteLine("4) Aplicar \"sem limite\" (0, convenção do próprio RTSS)");
    Console.WriteLine("5) Reverter ao estado capturado na opção 2");
    Console.WriteLine("0) Sair");
    Console.Write("> ");
}

static async Task ShowCurrentStateAsync(RtssFpsLimitModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Estado atual: {Describe(snapshot)}");
}

static async Task<ModuleSnapshot> CaptureAsync(RtssFpsLimitModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Capturado: {Describe(snapshot)}. Guarde isso de cabeça — é o que a opção 5 deve devolver.");
    return snapshot;
}

static async Task ApplyAsync(RtssFpsLimitModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Capture o estado atual primeiro (opção 2) — senão, depois de aplicar, não há para onde reverter.");
        return;
    }

    Console.Write("Novo limite de FPS (número inteiro positivo): ");
    string? input = Console.ReadLine();
    if (!int.TryParse(input, out int value) || value <= 0)
    {
        Console.WriteLine("Precisa ser um número inteiro positivo.");
        return;
    }

    await ApplyLimitAsync(module, value).ConfigureAwait(false);
}

static async Task ApplyNoLimitAsync(RtssFpsLimitModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Capture o estado atual primeiro (opção 2) — senão, depois de aplicar, não há para onde reverter.");
        return;
    }

    await ApplyLimitAsync(module, 0).ConfigureAwait(false);
}

static async Task ApplyLimitAsync(RtssFpsLimitModule module, int value)
{
    var desired = new DesiredEnvironment { FpsLimit = value };
    var outcome = await module.ApplyAsync(desired, CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine(outcome.Status switch
    {
        ApplyStatus.Changed =>
            $"Aplicado. Confira no osd do RTSS (ou no painel dele): o limite deveria estar em {(value == 0 ? "sem limite" : value.ToString(CultureInfo.InvariantCulture))} agora.",
        ApplyStatus.NothingToDo => "Inesperado: este teste sempre pede uma mudança.",
        _ => $"Falhou: {outcome.ErrorMessage}",
    });
}

static async Task RevertAsync(RtssFpsLimitModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Nada para reverter: capture o estado atual primeiro (opção 2).");
        return;
    }

    var outcome = await module.RevertAsync(captured, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine(outcome.Reverted
        ? $"Revertido. Confira no RTSS: o limite deveria estar exatamente como antes — {Describe(captured)}."
        : $"Falhou: {outcome.ErrorMessage}");
}

static string Describe(ModuleSnapshot snapshot)
{
    if (snapshot.Payload is not { } payload)
    {
        return "(sem dados)";
    }

    bool installed = payload["installed"]!.GetValue<bool>();
    if (!installed)
    {
        return "RTSS não instalado (nada para limitar)";
    }

    int limit = payload["framerateLimit"]!.GetValue<int>();
    return limit == 0 ? "RTSS instalado, sem limite de FPS" : $"RTSS instalado, limite de {limit} FPS";
}
