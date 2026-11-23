using SunshineHelper.Domain.Settings;

namespace SunshineHelper.Application.Channel;

/// <summary>
/// The already-loaded settings, as of right now. <see cref="TriggerChannelHandler"/> needs
/// a value synchronously (a connecting client cannot wait on settings.json I/O on top of
/// its own time budget), so loading, validating and keeping this current — on startup, and
/// whenever the user changes a profile through the UI (Phase 1, PR "Casca da interface") —
/// is someone else's job; this is deliberately just a read of whatever that someone last
/// published. <see cref="SunshineHelper.Application.Settings.SettingsService"/> does the
/// actual loading.
/// </summary>
public interface ICurrentSettingsProvider
{
    AppSettings Current { get; }
}
