using SunshineHelper.Application.Channel;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Settings;

/// <summary>
/// Keeps the settings the app is running with, readable without waiting
/// (<see cref="ICurrentSettingsProvider"/>). Whoever loads or edits settings.json calls
/// <see cref="Publish"/>; the pipe handler and the log monitor only ever read
/// <see cref="Current"/>, so a profile edit takes effect on the next connection without
/// restarting anything.
/// </summary>
public sealed class CurrentSettingsHolder : ICurrentSettingsProvider
{
    private volatile AppSettings _current;

    public CurrentSettingsHolder(AppSettings initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
    }

    public AppSettings Current => _current;

    /// <summary>Replaces the settings in use. Callers must have validated them first.</summary>
    public void Publish(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _current = settings;
    }
}
