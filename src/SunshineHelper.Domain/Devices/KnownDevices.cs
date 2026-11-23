namespace SunshineHelper.Domain.Devices;

/// <summary>The rules of the list of known devices, with no I/O: add a connection, forget
/// one, and keep the list tidy. Names are compared ignoring case, the same way profiles
/// match them (<c>ClientProfileSelector</c>).</summary>
public static class KnownDevices
{
    /// <summary>The oldest devices are dropped past this, so a hand-edited or hostile name
    /// source can never make the file grow without limit.</summary>
    public const int MaxDevices = 100;

    public const int MaxNameLength = 200;

    public static IReadOnlyList<KnownDevice> Record(
        IReadOnlyList<KnownDevice> current,
        string? name,
        string? profileName,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);

        string clean = Clean(name);
        if (clean.Length == 0)
        {
            return current;
        }

        List<KnownDevice> updated = [];
        bool found = false;
        foreach (var device in current)
        {
            if (string.Equals(device.Name, clean, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                updated.Add(device with
                {
                    LastSeen = now,
                    ConnectionCount = device.ConnectionCount + 1,
                    LastProfileName = profileName,
                });
            }
            else
            {
                updated.Add(device);
            }
        }

        if (!found)
        {
            updated.Add(new KnownDevice
            {
                Name = clean,
                FirstSeen = now,
                LastSeen = now,
                ConnectionCount = 1,
                LastProfileName = profileName,
            });
        }

        return Normalize(updated);
    }

    public static IReadOnlyList<KnownDevice> Forget(IReadOnlyList<KnownDevice> current, string? name)
    {
        ArgumentNullException.ThrowIfNull(current);

        return [.. current.Where(d => !string.Equals(d.Name, Clean(name), StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Drops blank names and repeats (keeping the most recent one), puts the most
    /// recently seen first and applies <see cref="MaxDevices"/>. Used on what was read from
    /// disk too, since the file may have been edited by hand.</summary>
    public static IReadOnlyList<KnownDevice> Normalize(IEnumerable<KnownDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        return
        [
            .. devices
                .Where(d => Clean(d.Name).Length > 0)
                .GroupBy(d => Clean(d.Name), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(d => d.LastSeen).First() with { Name = g.Key })
                .OrderByDescending(d => d.LastSeen)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxDevices),
        ];
    }

    private static string Clean(string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}
