using System.Globalization;
using System.Threading;
using SunshineHelper.Application.Session;
using SunshineHelper.Domain.Session;
using SunshineHelper.Domain.Settings;
using SunshineHelper.Infrastructure.Display;

// Ferramenta de teste manual do PrimaryDisplayModule — não faz parte do app, não é
// instalada e não entra no CI (sem monitor de verdade no runner do GitHub). Cobre o
// checklist de docs/testes-manuais.md, "PR 5a — Módulo de vídeo: resolução".
//
// Rodar (PowerShell comum, não precisa ser administrador, fora de uma transmissão):
//   cd tools/manual-tests/display-module
//   dotnet run
//
// Ou, da raiz do repositório:
//   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\display-module\run.ps1

var module = new PrimaryDisplayModule();
ModuleSnapshot? captured = null;

while (true)
{
    PrintMenu(captured);
    string? choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await ShowCurrentModeAsync(module).ConfigureAwait(false);
            break;

        case "2":
            captured = await CaptureAsync(module).ConfigureAwait(false);
            break;

        case "3":
            await ApplyAsync(module, captured).ConfigureAwait(false);
            break;

        case "4":
            await RevertAsync(module, captured).ConfigureAwait(false);
            break;

        case "5":
            await TryUnsupportedResolutionAsync(module).ConfigureAwait(false);
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
    Console.WriteLine("=== Teste manual: módulo de vídeo (resolução) ===");
    Console.WriteLine(captured is null
        ? "Nada capturado ainda — comece pela opção 2."
        : $"Capturado: {Describe(captured)} (é para onde a opção 4 deve devolver a tela)");
    Console.WriteLine("1) Mostrar o modo de vídeo atual");
    Console.WriteLine("2) Capturar o modo atual, antes de aplicar qualquer coisa");
    Console.WriteLine("3) Aplicar uma resolução (pede largura e altura)");
    Console.WriteLine("4) Reverter ao modo capturado na opção 2");
    Console.WriteLine("5) Testar uma resolução que o monitor não suporta (deve falhar sem travar a tela)");
    Console.WriteLine("0) Sair");
    Console.Write("> ");
}

static async Task ShowCurrentModeAsync(PrimaryDisplayModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Modo atual: {Describe(snapshot)}");
}

static async Task<ModuleSnapshot> CaptureAsync(PrimaryDisplayModule module)
{
    var snapshot = await module.CaptureAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Capturado: {Describe(snapshot)}. Guarde isso de cabeça — é o que a opção 4 deve devolver.");
    return snapshot;
}

static async Task ApplyAsync(PrimaryDisplayModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Capture o modo atual primeiro (opção 2) — senão, depois de aplicar, não há para onde reverter.");
        return;
    }

    int width = ReadInt("Largura");
    int height = ReadInt("Altura");

    var desired = new DesiredEnvironment { Resolution = new Resolution(width, height) };
    var outcome = await module.ApplyAsync(desired, CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine(outcome.Status switch
    {
        ApplyStatus.Changed =>
            $"Aplicado. Confira a tela: deveria estar em {width}x{height} agora, na mesma taxa de atualização de antes.",
        ApplyStatus.NothingToDo => "Inesperado: este teste sempre pede uma resolução.",
        _ => $"Falhou: {outcome.ErrorMessage}",
    });
}

static async Task RevertAsync(PrimaryDisplayModule module, ModuleSnapshot? captured)
{
    if (captured is null)
    {
        Console.WriteLine("Nada para reverter: capture o modo atual primeiro (opção 2).");
        return;
    }

    var outcome = await module.RevertAsync(captured, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine(outcome.Reverted
        ? $"Revertido. Confira a tela: deveria estar exatamente como antes — {Describe(captured)}."
        : $"Falhou: {outcome.ErrorMessage}");
}

static async Task TryUnsupportedResolutionAsync(PrimaryDisplayModule module)
{
    const int width = 7680;
    const int height = 4320;

    Console.WriteLine($"Pedindo {width}x{height} (8K) — a maioria dos monitores recusa isso.");
    var desired = new DesiredEnvironment { Resolution = new Resolution(width, height) };
    var outcome = await module.ApplyAsync(desired, CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine(outcome.Status == ApplyStatus.Failed
        ? $"Falhou como esperado, sem travar a tela: {outcome.ErrorMessage}"
        : $"Inesperado: o monitor aceitou {width}x{height} (resultado: {outcome.Status}). Se não era para aceitar, escolha uma resolução ainda maior e repita.");
}

static string Describe(ModuleSnapshot snapshot)
{
    if (snapshot.Payload is not { } payload)
    {
        return "(sem dados)";
    }

    int width = payload["width"]!.GetValue<int>();
    int height = payload["height"]!.GetValue<int>();
    int refreshRateHz = payload["refreshRateHz"]!.GetValue<int>();
    return $"{width}x{height} @ {refreshRateHz} Hz";
}

static int ReadInt(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        string? text = Console.ReadLine();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
        {
            return value;
        }

        Console.WriteLine("Digite um número inteiro maior que zero.");
    }
}
