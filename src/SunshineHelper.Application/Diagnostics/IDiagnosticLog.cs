namespace SunshineHelper.Application.Diagnostics;

public enum DiagnosticLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Where the app writes down what it decided and what each module answered, so a problem
/// seen on someone's PC can be understood afterwards (docs/diagnostico.md). Writing must
/// never throw and never slow a stream down: an implementation that cannot write simply
/// drops the line.
/// </summary>
public interface IDiagnosticLog
{
    /// <param name="area">Short source name, such as <c>channel</c> or <c>orchestrator</c>.</param>
    void Write(DiagnosticLevel level, string area, string message);
}

/// <summary>Used when nothing is listening (and by tests that do not care).</summary>
public sealed class NullDiagnosticLog : IDiagnosticLog
{
    public static NullDiagnosticLog Instance { get; } = new();

    public void Write(DiagnosticLevel level, string area, string message)
    {
    }
}
