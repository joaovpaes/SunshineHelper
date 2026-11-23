namespace SunshineHelper.Domain.Tests.TestSupport;

/// <summary>Reads files from the repository itself, found by walking up to the solution
/// file. For the few things the CI cannot exercise as code — the WPF resource
/// dictionaries are plain XML, so a test can still check them.</summary>
internal static class RepoFiles
{
    public static string ReadText(string relativePath)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SunshineHelper.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Não achei SunshineHelper.sln subindo a partir de '{AppContext.BaseDirectory}'.");
        }

        return File.ReadAllText(Path.Combine(dir, relativePath));
    }
}
