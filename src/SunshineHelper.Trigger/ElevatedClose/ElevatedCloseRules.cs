namespace SunshineHelper.Trigger.ElevatedClose;

/// <summary>
/// The rules the <c>elevated-close</c> mode checks on its own before it touches a process
/// (docs/adr/0017-fechar-jogo-de-administrador.md, section 3). It trusts nothing from the caller but the
/// process id and start time, reads no file of the user, and refuses at the first rule that fails.
/// The user's own lists (launcher, protected names, started before the session) are the app's job,
/// before it starts the task (ADR 0016).
/// </summary>
internal static class ElevatedCloseRules
{
    /// <summary>How far up the ancestry walk goes before giving up. Sunshine is two or three levels up
    /// in practice (game, launcher, sunshine.exe).</summary>
    public const int MaxAncestryDepth = 32;

    /// <summary>The executable name the ancestry walk looks for.</summary>
    public const string SunshineName = "sunshine";

    /// <summary>Names never closed, compiled in because this mode reads no settings. Must contain every
    /// name of <c>LauncherSettings.ForbiddenNames</c> and <c>CloseGameProtection.ExtraFixedNames</c>;
    /// a test keeps them in step.</summary>
    public static IReadOnlySet<string> ForbiddenNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SunshineHelper",
        "SunshineHelper.Trigger",
        "sunshine",
        "sunshinesvc",
        "explorer",
        "dwm",
        "csrss",
        "wininit",
        "winlogon",
        "lsass",
        "services",
        "smss",
        "svchost",
        "System",
        "Idle",
        "Registry",
        "fontdrvhost",
        "conhost",
        "ApplicationFrameHost",
    };

    /// <summary>Checks every rule and, only if all pass, does what <paramref name="request"/> asks.</summary>
    public static ElevatedCloseExitCode Run(ElevatedCloseRequest request, IElevatedProcessSystem system)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(system);

        bool forKill = request.Operation == ElevatedCloseOperation.Kill;
        OpenStatus status = system.Open(request.ProcessId, forKill, out ITargetProcess? opened);
        if (status == OpenStatus.NotFound)
        {
            return ElevatedCloseExitCode.NotFound;
        }

        if (status != OpenStatus.Opened || opened is null)
        {
            opened?.Dispose();
            return ElevatedCloseExitCode.NotInspectable;
        }

        using ITargetProcess target = opened;
        ElevatedCloseExitCode refusal = Check(request, target, system);
        if (refusal != ElevatedCloseExitCode.Done)
        {
            return refusal;
        }

        bool acted = forKill ? target.Kill() : target.RequestClose();
        return acted ? ElevatedCloseExitCode.Done : ElevatedCloseExitCode.ActionFailed;
    }

    /// <summary>Whether <paramref name="name"/> is on the fixed list, with or without <c>.exe</c>.</summary>
    public static bool IsForbidden(string name)
    {
        string normalized = NormalizeName(name);
        return normalized.Length == 0 || ForbiddenNames.Contains(normalized);
    }

    /// <summary>
    /// Whether a running <c>sunshine.exe</c> is among the ancestors of <paramref name="process"/>. Each
    /// parent must have started no later than its child: Windows keeps the parent's id after the parent
    /// exits, and a newer process holding that id is not the real parent.
    /// </summary>
    public static bool IsStartedBySunshine(ProcessNode process, IElevatedProcessSystem system)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(system);

        var seen = new HashSet<int> { process.ProcessId };
        ProcessNode child = process;
        for (int depth = 0; depth < MaxAncestryDepth; depth++)
        {
            int parentId = child.ParentProcessId;
            if (parentId <= 0 || !seen.Add(parentId))
            {
                return false;
            }

            ProcessNode? parent = system.Describe(parentId);
            if (parent is null || parent.ProcessId != parentId || parent.StartTime > child.StartTime)
            {
                return false;
            }

            if (string.Equals(NormalizeName(parent.Name), SunshineName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            child = parent;
        }

        return false;
    }

    private static ElevatedCloseExitCode Check(ElevatedCloseRequest request, ITargetProcess target, IElevatedProcessSystem system)
    {
        ProcessNode node = target.Node;
        if (node.ProcessId != request.ProcessId || node.StartTime != request.StartTime)
        {
            return ElevatedCloseExitCode.NotFound;
        }

        if (IsForbidden(node.Name))
        {
            return ElevatedCloseExitCode.ForbiddenName;
        }

        if (target.IsCritical)
        {
            return ElevatedCloseExitCode.Critical;
        }

        if (!target.IsElevated)
        {
            return ElevatedCloseExitCode.NotElevated;
        }

        if (!target.BelongsToCaller)
        {
            return ElevatedCloseExitCode.OtherUser;
        }

        if (!IsStartedBySunshine(node, system))
        {
            return ElevatedCloseExitCode.NotStartedBySunshine;
        }

        if (!target.HasVisibleWindow)
        {
            return ElevatedCloseExitCode.NoVisibleWindow;
        }

        return ElevatedCloseExitCode.Done;
    }

    private static string NormalizeName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }
}
