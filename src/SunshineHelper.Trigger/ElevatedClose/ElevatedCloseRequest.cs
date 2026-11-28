using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>What the <c>elevated-close</c> mode was asked to do.</summary>
internal enum ElevatedCloseOperation
{
    /// <summary>Post <c>WM_CLOSE</c> to the process's windows, like clicking their X.</summary>
    Close,

    /// <summary>End this one process (never its children).</summary>
    Kill,
}

/// <summary>
/// The parsed arguments of the <c>elevated-close</c> mode. The scheduled task passes them, but any
/// program of the user can start that task with values of its own choosing
/// (docs/adr/0017-fechar-jogo-de-administrador.md, "Riscos aceitos"), so parsing is strict: exactly
/// the verb, an operation and two positive whole numbers written with digits only.
/// </summary>
/// <param name="Operation">Close or kill.</param>
/// <param name="ProcessId">The target's process id.</param>
/// <param name="StartTime">The target's creation time as a Windows FILETIME (UTC, 100 ns units since
/// 1601), exactly as <c>GetProcessTimes</c> reports it.</param>
internal sealed record ElevatedCloseRequest(ElevatedCloseOperation Operation, int ProcessId, long StartTime)
{
    /// <summary>The first argument that selects this mode. The scheduled task always puts it first, so
    /// the task cannot reach any other mode of the auxiliary.</summary>
    public const string Verb = "elevated-close";

    /// <summary>Reads <c>elevated-close close|kill &lt;pid&gt; &lt;start time&gt;</c>; anything else,
    /// even one argument more or a sign or space in a number, fails.</summary>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out ElevatedCloseRequest? request)
    {
        request = null;
        if (args is null || args.Count != 4 || !string.Equals(args[0], Verb, StringComparison.Ordinal))
        {
            return false;
        }

        ElevatedCloseOperation operation;
        if (string.Equals(args[1], "close", StringComparison.Ordinal))
        {
            operation = ElevatedCloseOperation.Close;
        }
        else if (string.Equals(args[1], "kill", StringComparison.Ordinal))
        {
            operation = ElevatedCloseOperation.Kill;
        }
        else
        {
            return false;
        }

        // NumberStyles.None: digits only, no sign, no spaces, no thousands separator.
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int processId) || processId <= 0)
        {
            return false;
        }

        if (!long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out long startTime) || startTime <= 0)
        {
            return false;
        }

        request = new ElevatedCloseRequest(operation, processId, startTime);
        return true;
    }
}
