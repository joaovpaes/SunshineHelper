using System.Globalization;
using System.Text;

namespace SunshineHelper.Domain.Settings;

/// <summary>Everything the profile screen's fields hold, as the user typed it. Kept as
/// plain text and flags so the screen stays thin and the rules below are testable without
/// WPF.</summary>
public sealed record ProfileFormInput
{
    public string Name { get; init; } = string.Empty;

    /// <summary>One client name per line (commas and semicolons also accepted).</summary>
    public string ClientNamesText { get; init; } = string.Empty;

    public ProfileValueMode ResolutionMode { get; init; } = ProfileValueMode.FollowClient;

    /// <summary>For example <c>1920x1080</c>. Only read when the mode is Fixed.</summary>
    public string ResolutionText { get; init; } = string.Empty;

    public ProfileValueMode HdrMode { get; init; } = ProfileValueMode.Leave;

    public bool HdrOn { get; init; }

    public ProfileValueMode FpsMode { get; init; } = ProfileValueMode.Leave;

    public string FpsText { get; init; } = string.Empty;

    public bool RevertOnDisconnect { get; init; }

    /// <summary>Empty means "use the global launcher delay".</summary>
    public string LauncherDelayText { get; init; } = string.Empty;
}

/// <summary>What is wrong with a profile form; the screen maps each one to a message in
/// the user's language.</summary>
public enum ProfileFormError
{
    NameEmpty,
    ResolutionInvalid,
    FpsInvalid,
    DelayInvalid,
}

public sealed record ProfileFormResult(ClientProfile? Profile, IReadOnlyList<ProfileFormError> Errors)
{
    public bool IsValid => Profile is not null;
}

/// <summary>
/// Turns the profile screen's fields into a <see cref="ClientProfile"/> and back
/// (docs/adr/0009-tela-de-perfis.md). Every profile that comes out of <see cref="Build"/>
/// passes <see cref="SettingsValidator"/>.
/// </summary>
public static class ProfileForm
{
    public const int MaxResolutionSide = 16_384;
    public const int MaxFps = 1_000;

    public static ProfileFormResult Build(
        ProfileFormInput input,
        ClientProfile? existing,
        IEnumerable<string> otherProfileIds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(otherProfileIds);

        var errors = new List<ProfileFormError>();

        string name = input.Name.Trim();
        if (name.Length == 0)
        {
            errors.Add(ProfileFormError.NameEmpty);
        }

        var resolution = new ProfileValue<Resolution> { Mode = input.ResolutionMode };
        if (input.ResolutionMode == ProfileValueMode.Fixed)
        {
            if (TryParseResolution(input.ResolutionText, out var parsed))
            {
                resolution = resolution with { FixedValue = parsed };
            }
            else
            {
                errors.Add(ProfileFormError.ResolutionInvalid);
            }
        }

        var fps = new ProfileValue<int> { Mode = input.FpsMode };
        if (input.FpsMode == ProfileValueMode.Fixed)
        {
            if (int.TryParse(input.FpsText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                && value is >= 1 and <= MaxFps)
            {
                fps = fps with { FixedValue = value };
            }
            else
            {
                errors.Add(ProfileFormError.FpsInvalid);
            }
        }

        int? delay = null;
        string delayText = input.LauncherDelayText.Trim();
        if (delayText.Length > 0)
        {
            if (int.TryParse(delayText, NumberStyles.None, CultureInfo.InvariantCulture, out int ms)
                && ms <= LauncherSettings.MaxDelayMs)
            {
                delay = ms;
            }
            else
            {
                errors.Add(ProfileFormError.DelayInvalid);
            }
        }

        if (errors.Count > 0)
        {
            return new ProfileFormResult(null, errors);
        }

        var hdr = new ProfileValue<bool>
        {
            Mode = input.HdrMode,
            // For bool the fixed value is a plain bool (see SettingsValidator); it only
            // means something when the mode is Fixed.
            FixedValue = input.HdrMode == ProfileValueMode.Fixed && input.HdrOn,
        };

        // The id never changes once a profile exists: defaultProfileId points at it.
        string id = existing?.Id ?? UniqueId(Slugify(name), otherProfileIds);

        var profile = new ClientProfile
        {
            Id = id,
            Name = name,
            // IP ranges are not editable here; whatever the file holds is kept.
            Match = (existing?.Match ?? new ClientMatchRule()) with { ClientNames = ParseClientNames(input.ClientNamesText) },
            Display = new DisplayProfileSettings { Resolution = resolution, Hdr = hdr, FpsLimit = fps },
            LauncherDelayMs = delay,
            RevertOnDisconnect = input.RevertOnDisconnect,
        };

        return new ProfileFormResult(profile, []);
    }

    public static ProfileFormInput FromProfile(ClientProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var display = profile.Display;
        return new ProfileFormInput
        {
            Name = profile.Name,
            ClientNamesText = string.Join(Environment.NewLine, profile.Match.ClientNames),
            ResolutionMode = display.Resolution.Mode,
            ResolutionText = display.Resolution.FixedValue is { } r
                ? string.Create(CultureInfo.InvariantCulture, $"{r.Width}x{r.Height}")
                : string.Empty,
            HdrMode = display.Hdr.Mode,
            HdrOn = display.Hdr.FixedValue,
            FpsMode = display.FpsLimit.Mode,
            FpsText = display.FpsLimit.Mode == ProfileValueMode.Fixed
                ? display.FpsLimit.FixedValue.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            RevertOnDisconnect = profile.RevertOnDisconnect,
            LauncherDelayText = profile.LauncherDelayMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    /// <summary>Accepts <c>1920x1080</c>, <c>1920 x 1080</c> and <c>1920×1080</c>.</summary>
    public static bool TryParseResolution(string? text, out Resolution resolution)
    {
        resolution = new Resolution(0, 0);
        string[] parts = (text ?? string.Empty).Split(['x', 'X', '×'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            || width is < 1 or > MaxResolutionSide
            || height is < 1 or > MaxResolutionSide)
        {
            return false;
        }

        resolution = new Resolution(width, height);
        return true;
    }

    /// <summary>Client names as typed: one per line, commas or semicolons also split,
    /// trimmed, empty ones dropped, repeats (ignoring case) dropped.</summary>
    public static IReadOnlyList<string> ParseClientNames(string? text) =>
        (text ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>A file-safe id from the profile name: lowercase letters and digits
    /// separated by single dashes ("Sala TV 4K" becomes "sala-tv-4k"). "profile" if
    /// nothing usable is left.</summary>
    public static string Slugify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var builder = new StringBuilder();
        bool pendingDash = false;
        foreach (char c in name.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // The accent of "é", dropped so it becomes plain "e".
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.Length == 0 ? "profile" : builder.ToString();
    }

    private static string UniqueId(string baseId, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(baseId))
        {
            return baseId;
        }

        for (int n = 2; ; n++)
        {
            string candidate = string.Create(CultureInfo.InvariantCulture, $"{baseId}-{n}");
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
