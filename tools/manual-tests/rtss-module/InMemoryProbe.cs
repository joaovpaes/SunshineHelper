using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;

namespace SunshineHelper.Tools.RtssModuleHarness;

/// <summary>
/// Passo 1 da decisão D1 (docs/fase1-plano.md): o limite de FPS do RTSS vale só em memória?
/// Muda "FramerateLimit" do perfil global com SetProfileProperty + UpdateProfiles e NUNCA chama
/// SaveProfile, então o arquivo do perfil (em Program Files) não é tocado. Se o RTSS aplicar o
/// limite assim, o app não precisa de administrador para o FPS. Rode SEM administrador.
/// </summary>
internal static class InMemoryProbe
{
    private const string DefaultInstallDirectory = @"C:\Program Files (x86)\RivaTuner Statistics Server";

    // NUL-terminated ASCII (LPCSTR). The empty name is RTSS's "global profile".
    private static readonly byte[] _globalProfileName = "\0"u8.ToArray();
    private static readonly byte[] _framerateLimitProperty = "FramerateLimit\0"u8.ToArray();

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=== Teste do RTSS só em memória (D1, passo 1) ===");
        Console.WriteLine("Rode este teste SEM administrador. O arquivo do perfil não deve ser gravado.");

        bool isAdmin = IsAdministrator();
        Console.WriteLine(isAdmin
            ? "ATENÇÃO: este PowerShell está como administrador. O teste só vale sem administrador; feche e abra um comum."
            : "OK: sem administrador.");

        string? directory = FindInstallDirectory();
        if (directory is null)
        {
            Console.WriteLine("Não achei o RTSS (RTSSHooks64.dll). Abra o RTSS e tente de novo.");
            return;
        }

        Console.WriteLine($"Pasta do RTSS: {directory}");
        RtssNative.EnsureResolver(directory);

        string profileFile = Path.Combine(directory, "Profiles", "Global");
        string hashBefore = HashOf(profileFile);
        Console.WriteLine($"Arquivo do perfil global antes: {hashBefore}");

        if (!TryRead(out int original))
        {
            Console.WriteLine("Não consegui ler o limite atual (GetProfileProperty). Teste interrompido.");
            return;
        }

        Console.WriteLine($"Limite atual do RTSS: {Describe(original)}");
        Console.WriteLine("Antes de continuar, deixe um jogo ou programa com o OSD do RTSS ligado rodando, para ver o FPS.");
        Console.Write("Limite de teste (número inteiro positivo, ex.: 30; precisa ser diferente do atual): ");
        if (!int.TryParse(Console.ReadLine(), NumberStyles.None, CultureInfo.InvariantCulture, out int target) || target <= 0 || target == original)
        {
            Console.WriteLine("Precisa ser um número inteiro positivo e diferente do limite atual.");
            return;
        }

        bool memoryAccepted = false;
        bool readBack = false;
        try
        {
            RtssNative.LoadProfile(_globalProfileName);
            memoryAccepted = TryWrite(target);
            readBack = TryRead(out int inMemory) && inMemory == target;
            RtssNative.UpdateProfiles();

            Console.WriteLine($"SetProfileProperty aceitou: {Answer(memoryAccepted)}. Releitura em memória: {Answer(readBack)}.");
            Console.WriteLine($"Agora olhe o jogo: o FPS está travado em {target.ToString(CultureInfo.InvariantCulture)}? (espere uns 3 segundos)");
            Console.Write("Respondeu ao limite? (s/n): ");
            bool applied = string.Equals(Console.ReadLine()?.Trim(), "s", StringComparison.OrdinalIgnoreCase);

            string hashAfter = HashOf(profileFile);
            bool fileUntouched = string.Equals(hashBefore, hashAfter, StringComparison.Ordinal);
            Console.WriteLine($"Arquivo do perfil global depois: {hashAfter} ({(fileUntouched ? "igual ao de antes" : "MUDOU")})");

            PrintVerdict(isAdmin, memoryAccepted, readBack, applied, fileUntouched);
        }
        finally
        {
            // Put the in-memory value back. Never SaveProfile: the file stays as it was.
            RtssNative.LoadProfile(_globalProfileName);
            _ = TryWrite(original);
            RtssNative.UpdateProfiles();
            Console.WriteLine($"Limite devolvido em memória a: {Describe(original)}. Confira no jogo que o FPS voltou ao normal.");
        }
    }

    private static void PrintVerdict(bool isAdmin, bool memoryAccepted, bool readBack, bool applied, bool fileUntouched)
    {
        Console.WriteLine();
        Console.WriteLine("----- Copie e cole no chat -----");
        Console.WriteLine($"Teste RTSS em memória: administrador={Answer(isAdmin)}; aceitou={Answer(memoryAccepted)}; releitura={Answer(readBack)}; jogo respondeu={Answer(applied)}; arquivo intocado={Answer(fileUntouched)}");
        Console.WriteLine("--------------------------------");
        Console.WriteLine(applied && fileUntouched && !isAdmin
            ? "Resultado: o limite vale só em memória, sem administrador. O app pode usar esse caminho."
            : "Resultado: não serve como está (veja as respostas acima). Envie a linha copiada.");
    }

    private static bool TryRead(out int value)
    {
        value = 0;
        nint data = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            if (!RtssNative.GetProfileProperty(_framerateLimitProperty, data, sizeof(int)))
            {
                return false;
            }

            value = Marshal.ReadInt32(data);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static bool TryWrite(int value)
    {
        nint data = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(data, value);

            // The pointer must stay valid until UpdateProfiles has run: RTSS may read through
            // it after SetProfileProperty returns (the same lesson as the module's own fix).
            bool accepted = RtssNative.SetProfileProperty(_framerateLimitProperty, data, sizeof(int));
            RtssNative.UpdateProfiles();
            return accepted;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static string Describe(int limit)
        => limit == 0 ? "sem limite" : $"{limit.ToString(CultureInfo.InvariantCulture)} FPS";

    private static string Answer(bool value) => value ? "sim" : "não";

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string HashOf(string path)
    {
        if (!File.Exists(path))
        {
            return "(arquivo não encontrado)";
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash)[..12];
    }

    private static string? FindInstallDirectory()
    {
        foreach (var process in Process.GetProcessesByName("RTSS"))
        {
            using (process)
            {
                try
                {
                    string? folder = Path.GetDirectoryName(process.MainModule?.FileName);
                    if (folder is not null && File.Exists(Path.Combine(folder, RtssNative.DllFileName)))
                    {
                        return folder;
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // MainModule can refuse access; the default path is still there to try.
                }
            }
        }

        return File.Exists(Path.Combine(DefaultInstallDirectory, RtssNative.DllFileName)) ? DefaultInstallDirectory : null;
    }
}
