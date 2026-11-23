using System.Globalization;
using System.Text;
using System.Threading;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Domain.Diagnostics;

namespace SunshineHelper.Infrastructure.Diagnostics;

/// <summary>
/// Appends one redacted line per event to <c>%LOCALAPPDATA%\SunshineHelper\logs\app.log</c>.
/// When the file passes <see cref="DefaultMaxBytes"/> it becomes <c>app.log.1</c> (the
/// previous one is dropped), so the log can never grow without limit. Any I/O problem is
/// swallowed: a log that cannot be written must not stop a stream.
/// </summary>
public sealed class FileDiagnosticLog : IDiagnosticLog
{
    public const long DefaultMaxBytes = 1_000_000;

    private readonly string _path;
    private readonly long _maxBytes;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    public FileDiagnosticLog(string path, long maxBytes = DefaultMaxBytes, TimeProvider? time = null)
    {
        _path = path;
        _maxBytes = maxBytes;
        _time = time ?? TimeProvider.System;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SunshineHelper",
        "logs",
        "app.log");

    public void Write(DiagnosticLevel level, string area, string message)
    {
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} [{level.ToString().ToUpperInvariant()}] {area}: {LogRedactor.Redact(message)}")
            .ReplaceLineEndings(" | ");

        try
        {
            lock (_lock)
            {
                string? directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();
                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Dropped on purpose: see the class remarks.
        }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < _maxBytes)
        {
            return;
        }

        string previous = _path + ".1";
        File.Move(_path, previous, overwrite: true);
    }
}
