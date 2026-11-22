using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Infrastructure.Display;

// Ferramenta de teste manual do HdrModule — não faz parte do app, não é instalada e não
// entra no CI (sem monitor de verdade no runner do GitHub). Cobre o checklist de
// docs/testes-manuais.md, "PR 5b — Módulo de HDR".
//
// Rodar (PowerShell comum, não precisa ser administrador, fora de uma transmissão):
//   cd tools/manual-tests/hdr-module
//   dotnet run
//
// Ou, da raiz do repositório:
//   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\hdr-module\run.ps1

var module = new HdrModule();
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
            await ApplyAsync(module, captured, enable: true).ConfigureAwait(false);
            break;

        case "4":
            await ApplyAsync(module, captured, enable: false).ConfigureAwait(false);
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
    Console.WriteLine("=== Teste manual: módulo de HDR ===");
    Console.WriteLine(captured is null
        ? "Nada capturado ainda — comece pela opção 2."
        : $"Capturado: HDR {Describe(captured)} (é para onde a opção 5 deve devolver)");
    Console.WriteLine("1) Mostrar o estado atual do HDR");
    Console.WriteLine("2) Capturar o estado atual, antes de aplicar qualquer coisa");
    Console.WriteLine("3) Ligar o HDR");
    Console.WriteLine("4) Desligar o HDR");
    Console.WriteLine("5) Reverter ao estado capturado na opção 2");
    Console.WriteLine("0) Sair");
    Console.Write("> ");
}

static async Task ShowCurrentStateAsync(HdrModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Estado atual: HDR {Describe(snapshot)}");
}

static async Task<ModuleSnapshot> CaptureAsync(HdrModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Capturado: HDR {Describe(snapshot)}. Guarde isso de cabeça — é o que a opção 5 deve devolver.");
    return snapshot;
}

static async Task ApplyAsync(HdrModule module, ModuleSnapshot? captured, bool enable)
{
    if (captured is null)
    {
        Console.WriteLine("Capture o estado atual primeiro (opção 2) — senão, depois de aplicar, não há para onde reverter.");
        return;
    }

    var desired = new DesiredEnvironment { Hdr = enable };
    var outcome = await module.ApplyAsync(desired, CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine(outcome.Status switch
    {
        ApplyStatus.Changed =>
            $"Aplicado. Confira em Configurações > Sistema > Tela: o HDR deveria estar {(enable ? "ligado" : "desligado")} agora.",
        ApplyStatus.NothingToDo => "Inesperado: este teste sempre pede uma mudança.",
        _ => $"Falhou: {outcome.ErrorMessage}",
    });
}

static async Task RevertAsync(HdrModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Nada para reverter: capture o estado atual primeiro (opção 2).");
        return;
    }

    var outcome = await module.RevertAsync(captured, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine(outcome.Reverted
        ? $"Revertido. Confira em Configurações > Sistema > Tela: o HDR deveria estar exatamente como antes — {Describe(captured)}."
        : $"Falhou: {outcome.ErrorMessage}");
}

static string Describe(ModuleSnapshot snapshot)
{
    if (snapshot.Payload is not { } payload)
    {
        return "(sem dados)";
    }

    bool enabled = payload["enabled"]!.GetValue<bool>();
    return enabled ? "ligado" : "desligado";
}
