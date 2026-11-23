using SunshineHelper.Domain.Session;

namespace SunshineHelper.Domain.Settings;

/// <summary>
/// Picks the profile for a connecting client. Pure, like
/// <see cref="Session.DesiredEnvironmentResolver"/>, so the matching rules are testable on
/// their own.
/// </summary>
/// <remarks>
/// Signal order follows docs/adr/0002-arquitetura-do-nucleo.md, "Escolha do perfil", as far
/// as this PR goes: client name, then the default profile. The next two signals the ADR
/// lists — IP range, and asking on the first connection — are deliberately not here yet
/// (docs/adr/0003-canal-e-auxiliar-de-gatilho.md); <see cref="ClientMatchRule.IpRanges"/>
/// already exists in settings.json for forward compatibility, but nothing reads it until
/// that follow-up lands.
/// </remarks>
public static class ClientProfileSelector
{
    /// <summary>Null means no profile matched the client's name and no
    /// <see cref="AppSettings.DefaultProfileId"/> is configured — the caller decides what
    /// that means for its own op (the channel handler turns it into a clear error instead
    /// of guessing a profile).</summary>
    public static ClientProfile? Select(AppSettings settings, ClientRequest request)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name is { Length: > 0 } name)
        {
            foreach (var profile in settings.Profiles)
            {
                if (MatchesName(profile.Match, name))
                {
                    return profile;
                }
            }
        }

        if (settings.DefaultProfileId is { } defaultId)
        {
            foreach (var profile in settings.Profiles)
            {
                if (string.Equals(profile.Id, defaultId, StringComparison.Ordinal))
                {
                    return profile;
                }
            }
        }

        return null;
    }

    private static bool MatchesName(ClientMatchRule match, string clientName)
    {
        foreach (var candidate in match.ClientNames)
        {
            if (string.Equals(candidate, clientName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
