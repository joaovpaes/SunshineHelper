namespace SunshineHelper.Domain.Settings;

/// <summary>Structural validation of an already-deserialized <see cref="AppSettings"/>.
/// Pure and I/O-free, so it is usable from Domain, Application or the UI alike. See
/// docs/adr/0002-arquitetura-do-nucleo.md.</summary>
public static class SettingsValidator
{
    public static SettingsValidationResult Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.SchemaVersion != SettingsSchema.CurrentVersion)
        {
            errors.Add(
                $"schemaVersion {settings.SchemaVersion} não é suportado (esperado {SettingsSchema.CurrentVersion}).");
        }

        ValidateTiming(settings.Timing, errors);
        ValidateProfiles(settings, errors);

        return errors.Count == 0 ? SettingsValidationResult.Valid : new SettingsValidationResult(errors);
    }

    private static void ValidateTiming(TimingSettings timing, List<string> errors)
    {
        if (timing.ApplyTargetMs <= 0)
        {
            errors.Add("timing.applyTargetMs deve ser maior que zero.");
        }

        if (timing.BarrierDefaultMs < timing.ApplyTargetMs)
        {
            errors.Add("timing.barrierDefaultMs não pode ser menor que timing.applyTargetMs.");
        }

        if (timing.BarrierCeilingMs < timing.BarrierDefaultMs)
        {
            errors.Add("timing.barrierCeilingMs não pode ser menor que timing.barrierDefaultMs.");
        }

        if (timing.LauncherDelayMs < 0)
        {
            errors.Add("timing.launcherDelayMs não pode ser negativo.");
        }
    }

    private static void ValidateProfiles(AppSettings settings, List<string> errors)
    {
        // An empty 'profiles' list is valid: it is exactly what AppSettings.CreateDefault()
        // produces for a brand-new install, before the user sets up a profile in the UI.
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in settings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                errors.Add("Um perfil está sem 'id'.");
                continue;
            }

            if (!seenIds.Add(profile.Id))
            {
                errors.Add($"Id de perfil duplicado: '{profile.Id}'.");
            }

            ValidateDisplay(profile, errors);

            if (profile.LauncherDelayMs is { } delay && delay < 0)
            {
                errors.Add($"Perfil '{profile.Id}': launcherDelayMs não pode ser negativo.");
            }
        }

        if (settings.DefaultProfileId is { } defaultId &&
            !settings.Profiles.Any(p => string.Equals(p.Id, defaultId, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"defaultProfileId '{defaultId}' não corresponde a nenhum perfil em 'profiles'.");
        }
    }

    private static void ValidateDisplay(ClientProfile profile, List<string> errors)
    {
        var display = profile.Display;

        if (display.Resolution.Mode == ProfileValueMode.Fixed)
        {
            var resolution = display.Resolution.FixedValue;
            if (resolution is null || resolution.Width <= 0 || resolution.Height <= 0)
            {
                errors.Add($"Perfil '{profile.Id}': resolução fixa inválida.");
            }
        }

        if (display.FpsLimit.Mode == ProfileValueMode.Fixed &&
            display.FpsLimit.FixedValue is null or <= 0)
        {
            errors.Add($"Perfil '{profile.Id}': fpsLimit fixo deve ser maior que zero.");
        }
    }
}
