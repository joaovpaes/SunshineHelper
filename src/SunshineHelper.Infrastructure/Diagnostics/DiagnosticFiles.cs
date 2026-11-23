using System.Text;

namespace SunshineHelper.Infrastructure.Diagnostics;

/// <summary>
/// Reads the files a diagnostic report needs. Opens with full sharing because the app and
/// Sunshine keep their logs open for writing, and never throws for a missing or locked file:
/// a report with a gap is better than no report.
/// </summary>
public static class DiagnosticFiles
{
    public const int MaxTextBytes = 256 * 1024;
    public const int MaxTailBytes = 1024 * 1024;

    /// <summary>The first <paramref name="maxBytes"/> of a small text file; null when it cannot be read.</summary>
    public static string? ReadText(string path, int maxBytes = MaxTextBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = Open(path);
            byte[] buffer = new byte[(int)Math.Min(maxBytes, stream.Length)];
            int read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The last <paramref name="maxLines"/> non-empty lines of a log (looking only at its
    /// last <paramref name="maxBytes"/>), oldest first; null when it cannot be read.</summary>
    public static IReadOnlyList<string>? ReadLastLines(string path, int maxLines, int maxBytes = MaxTailBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        try
        {
            using var stream = Open(path);
            long length = stream.Length;
            long start = Math.Max(0, length - maxBytes);
            stream.Seek(start, SeekOrigin.Begin);
            byte[] buffer = new byte[(int)(length - start)];
            int read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

            List<string> lines = Encoding.UTF8.GetString(buffer, 0, read)
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Length > 0)
                .ToList();

            // Starting in the middle of the file, the first line is cut: drop it.
            if (start > 0 && lines.Count > 0)
            {
                lines.RemoveAt(0);
            }

            return lines.Count <= maxLines ? lines : lines.GetRange(lines.Count - maxLines, maxLines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
