namespace SunshineHelper.Domain.Tests.TestSupport;

/// <summary>Locates <c>tests/fixtures</c> from wherever the test assembly actually runs
/// (different per OS, per configuration, and between a local build and CI), by walking
/// up from the running assembly until it finds the solution file.</summary>
internal static class FixtureFiles
{
    public static string ReadSunshineLog(string fileName)
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

        return File.ReadAllText(Path.Combine(dir, "tests", "fixtures", "sunshine", fileName));
    }
}
