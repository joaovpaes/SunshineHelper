using SunshineHelper.Application.Launcher;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.CloseGame;

/// <summary>Why a process may not be closed by the close-game combo.</summary>
public enum CloseGameProtectionReason
{
    /// <summary>Not protected: it may be closed.</summary>
    None,

    /// <summary>Windows, Sunshine, this app, or a host of store apps.</summary>
    Fixed,

    /// <summary>One of the user's launcher names (Playnite, for instance).</summary>
    Launcher,

    /// <summary>On the user's own list of names to protect.</summary>
    UserList,

    /// <summary>It was already running before the stream began.</summary>
    StartedBeforeSession,
}

/// <summary>
/// The rules that keep the close-game combo off everything but the game
/// (docs/adr/0016-fechar-jogo-pelo-controle.md, "Quem é o alvo"). Pure: the same answer for
/// the same process, settings and session start.
/// </summary>
public static class CloseGameProtection
{
    /// <summary>Slack when comparing start times, for clocks that disagree by a moment.</summary>
    public static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);

    /// <summary>Names protected here on top of <see cref="LauncherSettings.ForbiddenNames"/>.
    /// ApplicationFrameHost hosts the windows of many store apps at once, so closing it
    /// could take down more than the game.</summary>
    public static IReadOnlySet<string> ExtraFixedNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost",
    };

    /// <summary>Whether <paramref name="process"/> may be closed, given the settings in use
    /// and when the session began.</summary>
    public static CloseGameProtectionReason Check(ProcessIdentity process, DateTimeOffset sessionStartedAt, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(process);

        var byName = CheckName(process.Name, settings);
        if (byName != CloseGameProtectionReason.None)
        {
            return byName;
        }

        return process.StartedAt < sessionStartedAt - StartTolerance
            ? CloseGameProtectionReason.StartedBeforeSession
            : CloseGameProtectionReason.None;
    }

    /// <summary>The name-only part of <see cref="Check"/>, also used for the children of the
    /// game before ending its whole process tree.</summary>
    public static CloseGameProtectionReason CheckName(string processName, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(processName);
        ArgumentNullException.ThrowIfNull(settings);

        string name = LauncherSettings.NormalizeName(processName);
        if (name.Length == 0)
        {
            return CloseGameProtectionReason.Fixed;
        }

        if (LauncherSettings.ForbiddenNames.Contains(name) || ExtraFixedNames.Contains(name))
        {
            return CloseGameProtectionReason.Fixed;
        }

        if (Matches(settings.Launcher.ProcessNames, name))
        {
            return CloseGameProtectionReason.Launcher;
        }

        return Matches(settings.Controller.CloseGame.ProtectedNames, name)
            ? CloseGameProtectionReason.UserList
            : CloseGameProtectionReason.None;
    }

    private static bool Matches(IReadOnlyList<string> names, string normalized)
    {
        foreach (string candidate in names)
        {
            if (candidate is not null &&
                string.Equals(LauncherSettings.NormalizeName(candidate), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
