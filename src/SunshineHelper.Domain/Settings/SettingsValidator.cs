using System.Globalization;
using SunshineHelper.Domain.Gamepad;

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
        ValidateLauncher(settings.Launcher, errors);
        ValidateController(settings.Controller, errors);

        return errors.Count == 0 ? SettingsValidationResult.Valid : new SettingsValidationResult(errors);
    }

    private static void ValidateLauncher(LauncherSettings launcher, List<string> errors)
    {
        if (launcher.CloseTimeoutMs <= 0)
        {
            errors.Add("launcher.closeTimeoutMs deve ser maior que zero.");
        }

        ValidateProcessNames(launcher.ProcessNames, "launcher.processNames", rejectForbidden: true, errors);
    }

    /// <summary>Checks a list of process names typed by the user. The launcher list rejects
    /// Windows, Sunshine and app names (closing them would be a disaster); a list of names to
    /// <i>protect</i> accepts them, since protecting them again is harmless.</summary>
    private static void ValidateProcessNames(IReadOnlyList<string> names, string field, bool rejectForbidden, List<string> errors)
    {
        if (names is null)
        {
            errors.Add($"{field} não pode ser null.");
            return;
        }

        foreach (var name in names)
        {
            string normalized = name is null ? string.Empty : LauncherSettings.NormalizeName(name);
            if (normalized.Length == 0)
            {
                errors.Add($"{field} tem um nome vazio.");
            }
            else if (rejectForbidden && LauncherSettings.ForbiddenNames.Contains(normalized))
            {
                errors.Add($"{field}: '{normalized}' é um processo do Windows, do Sunshine ou deste app e nunca pode ser encerrado.");
            }
            else if (normalized.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            {
                errors.Add($"{field}: '{normalized}' não é um nome de processo (use só o nome, como no Gerenciador de Tarefas, sem pasta).");
            }
        }
    }

    private static void ValidateController(ControllerSettings controller, List<string> errors)
    {
        if (controller is null)
        {
            errors.Add("controller não pode ser null.");
            return;
        }

        var closeGame = controller.CloseGame;
        var overlay = controller.Overlay;
        if (closeGame is null || overlay is null)
        {
            errors.Add("controller.closeGame e controller.overlay não podem ser null.");
            return;
        }

        errors.AddRange(ValidateCombo(closeGame.Combo, "controller.closeGame.combo"));
        errors.AddRange(ValidateCombo(overlay.Combo, "controller.overlay.combo"));

        if (closeGame.Combo is not null && overlay.Combo is not null &&
            closeGame.Combo.Buttons is not null && overlay.Combo.Buttons is not null &&
            closeGame.Combo.ToMask() == overlay.Combo.ToMask())
        {
            errors.Add("O combo de fechar o jogo e o do overlay não podem ter os mesmos botões.");
        }

        if (closeGame.PoliteTimeoutMs is < CloseGameSettings.MinPoliteTimeoutMs or > CloseGameSettings.MaxPoliteTimeoutMs)
        {
            errors.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"controller.closeGame.politeTimeoutMs deve ficar entre {CloseGameSettings.MinPoliteTimeoutMs} e {CloseGameSettings.MaxPoliteTimeoutMs} ms."));
        }

        ValidateProcessNames(closeGame.ProtectedNames, "controller.closeGame.protectedNames", rejectForbidden: false, errors);
    }

    /// <summary>The rules every combo follows (ADR 0015): 2 to 4 distinct known buttons,
    /// never Start and Back together (Start + Select, and Moonlight's quit shortcut), and a
    /// hold between <see cref="ComboSettings.MinHoldMs"/> and
    /// <see cref="ComboSettings.MaxHoldMs"/>. Public so the combo screen can show the same
    /// messages before saving.</summary>
    public static IReadOnlyList<string> ValidateCombo(ComboSettings combo, string field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var errors = new List<string>();
        if (combo is null || combo.Buttons is null)
        {
            errors.Add($"{field} está vazio.");
            return errors;
        }

        bool allKnown = true;
        foreach (var button in combo.Buttons)
        {
            if (!GamepadButtonMask.IsSingleButton(button))
            {
                allKnown = false;
                errors.Add($"{field}: '{button}' não é um botão aceito (o Guide e os gatilhos não entram em combos).");
            }
        }

        if (allKnown && combo.Buttons.Distinct().Count() != combo.Buttons.Count)
        {
            errors.Add($"{field} repete um botão.");
        }

        int count = GamepadButtonMask.Count(combo.ToMask());
        if (count is < ComboSettings.MinButtons or > ComboSettings.MaxButtons)
        {
            errors.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{field} precisa de {ComboSettings.MinButtons} a {ComboSettings.MaxButtons} botões diferentes."));
        }

        GamepadButtons startAndBack = GamepadButtons.Start | GamepadButtons.Back;
        if ((combo.ToMask() & startAndBack) == startAndBack)
        {
            errors.Add($"{field} não pode ter Start e Select (Back) juntos: é o atalho do Moonlight e de muitos jogos.");
        }

        if (combo.HoldMs is < ComboSettings.MinHoldMs or > ComboSettings.MaxHoldMs)
        {
            errors.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{field}.holdMs deve ficar entre {ComboSettings.MinHoldMs} e {ComboSettings.MaxHoldMs} ms."));
        }

        return errors;
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

        if (timing.LauncherDelayMs > LauncherSettings.MaxDelayMs)
        {
            errors.Add($"timing.launcherDelayMs não pode passar de {LauncherSettings.MaxDelayMs} ms.");
        }

        if (timing.LogPollIntervalMs <= 0)
        {
            errors.Add("timing.logPollIntervalMs deve ser maior que zero.");
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

        // FixedValue is `int`, not `int?`, here: an unconstrained generic's `T?` only
        // carries a real nullable annotation for reference-type instantiations (like
        // Resolution above); for a value type like int it collapses to plain T. An
        // unset FixedValue therefore defaults to 0, which this same check already
        // catches — no separate "is it set" check is needed or even possible.
        if (display.FpsLimit.Mode == ProfileValueMode.Fixed && display.FpsLimit.FixedValue <= 0)
        {
            errors.Add($"Perfil '{profile.Id}': fpsLimit fixo deve ser maior que zero.");
        }
    }
}
