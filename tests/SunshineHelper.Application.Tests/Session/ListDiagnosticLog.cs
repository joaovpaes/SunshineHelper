using SunshineHelper.Application.Diagnostics;

namespace SunshineHelper.Application.Tests.Session;

/// <summary>Keeps every diagnostic line in memory so a test can read what was written.</summary>
internal sealed class ListDiagnosticLog : IDiagnosticLog
{
    public List<(DiagnosticLevel Level, string Area, string Message)> Entries { get; } = [];

    public void Write(DiagnosticLevel level, string area, string message) =>
        Entries.Add((level, area, message));

    public bool Contains(DiagnosticLevel level, string text) =>
        Entries.Any(entry => entry.Level == level && entry.Message.Contains(text, StringComparison.Ordinal));
}
