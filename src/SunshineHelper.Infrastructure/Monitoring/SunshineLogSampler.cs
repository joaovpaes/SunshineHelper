using System.Text;

namespace SunshineHelper.Infrastructure.Monitoring;

/// <summary>
/// Reads a small piece of the start and of the end of Sunshine's log so the app can judge
/// its level of detail without loading a file that may be many megabytes. The configuration
/// is printed near the start; recent Debug lines are at the end. Opens with full sharing
/// because Sunshine keeps the file open for writing.
/// </summary>
public static class SunshineLogSampler
{
    private const int SampleBytes = 128 * 1024;

    /// <summary>Empty when the file is missing or cannot be read: that is not a problem
    /// worth a warning (Sunshine may not have started yet).</summary>
    public static IReadOnlyList<string> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var lines = new List<string>();
            AddChunk(stream, 0, lines);

            long tailStart = stream.Length - SampleBytes;
            if (tailStart > SampleBytes)
            {
                AddChunk(stream, tailStart, lines);
            }

            return lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void AddChunk(FileStream stream, long start, List<string> lines)
    {
        stream.Seek(start, SeekOrigin.Begin);
        byte[] buffer = new byte[(int)Math.Min(SampleBytes, stream.Length - start)];
        int read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        string text = Encoding.UTF8.GetString(buffer, 0, read);
        lines.AddRange(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
}
