namespace SunshineHelper.Domain.Session;

/// <summary>
/// Decides what to restore from a snapshot left on disk. A pure function, so the rule
/// that matters most — what happens after a crash or a reboot — is fully testable
/// without Windows.
/// </summary>
public static class RecoveryDecider
{
    /// <summary>Builds the plan for <paramref name="snapshot"/>.</summary>
    /// <param name="snapshot">The snapshot found on disk.</param>
    /// <param name="current">The current boot identity, or null when it cannot be
    /// determined.</param>
    /// <param name="now">The current time, used to sanity-check the boot identity.</param>
    public static RecoveryPlan Decide(SessionSnapshot snapshot, BootIdentity? current, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        bool rebootDetected = DetectReboot(snapshot, current, now);
        var toRevert = new List<CapturedModule>();
        var leftAlone = new List<CapturedModule>();

        foreach (var captured in snapshot.Modules.OrderByDescending(entry => entry.Order))
        {
            if (!captured.NeedsRevert)
            {
                leftAlone.Add(captured);
            }
            else if (rebootDetected && !captured.SurvivesReboot)
            {
                // Windows already restored this one when it booted; re-applying the
                // snapshot now would overwrite whatever the user set since.
                leftAlone.Add(captured);
            }
            else
            {
                toRevert.Add(captured);
            }
        }

        return new RecoveryPlan
        {
            RebootDetected = rebootDetected,
            State = snapshot.State,
            ModulesToRevert = toRevert,
            ModulesLeftAlone = leftAlone,
        };
    }

    /// <summary>
    /// Whether the PC restarted since the snapshot was written. Unsure counts as "same
    /// boot", which means reverting everything: a needless revert puts the environment
    /// back to the values the user themselves had, while a missed revert leaves the PC
    /// in streaming configuration — at the wrong resolution, with HDR and an FPS cap the
    /// user did not ask for.
    /// </summary>
    private static bool DetectReboot(SessionSnapshot snapshot, BootIdentity? current, DateTimeOffset now)
    {
        if (current is null)
        {
            return false;
        }

        // Uptime never decreases within a boot, so this is conclusive.
        if (current.UptimeMs < snapshot.Boot.UptimeMs)
        {
            return true;
        }

        if (string.Equals(current.Id, snapshot.Boot.Id, StringComparison.Ordinal))
        {
            return false;
        }

        // The ids differ, but that alone is not proof: an adjustment of the system clock
        // can change a computed identity without the PC having restarted. The PC cannot
        // have been up the whole time if it has been up for less than the time elapsed
        // since the snapshot was taken.
        double elapsedMs = (now - snapshot.CapturedAt).TotalMilliseconds;
        return current.UptimeMs < elapsedMs;
    }
}
