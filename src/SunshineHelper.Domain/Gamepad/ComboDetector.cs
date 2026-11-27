namespace SunshineHelper.Domain.Gamepad;

/// <summary>
/// Decides when a controller combo fires (docs/adr/0015-overlay-pelo-controle.md, "Máquina
/// de estados do combo"). Pure: the caller passes each reading with its own monotonic
/// timestamp, so tests drive it with made-up sequences and no clock or controller.
/// <para>Per controller: <b>Idle</b> → <b>Holding</b> (exactly a combo's buttons are down)
/// → fires <b>once</b> when the hold time is reached → <b>Waiting for release</b>, which
/// only goes back to Idle when every button of that combo is up. An extra button, letting
/// go of one, or the controller disconnecting cancels the count.</para>
/// Not thread-safe: one reader thread calls it.
/// </summary>
public sealed class ComboDetector
{
    /// <summary>XInput reports at most four controllers.</summary>
    public const int MaxControllers = 4;

    private readonly ComboDefinition[] _combos;
    private readonly ControllerState[] _states = new ControllerState[MaxControllers];

    public ComboDetector(IEnumerable<ComboDefinition> combos)
    {
        ArgumentNullException.ThrowIfNull(combos);

        _combos = [.. combos];
        var seenMasks = new HashSet<GamepadButtons>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var combo in _combos)
        {
            ArgumentNullException.ThrowIfNull(combo, nameof(combos));

            GamepadButtons mask = combo.Buttons & GamepadButtonMask.AllDigital;
            if (mask == GamepadButtons.None || mask != combo.Buttons)
            {
                throw new ArgumentException($"O combo '{combo.Id}' não tem botões válidos.", nameof(combos));
            }

            if (combo.Hold < TimeSpan.Zero)
            {
                throw new ArgumentException($"O combo '{combo.Id}' tem tempo de segurar negativo.", nameof(combos));
            }

            if (!seenMasks.Add(mask))
            {
                throw new ArgumentException($"Dois combos usam os mesmos botões ('{combo.Id}').", nameof(combos));
            }

            if (!seenIds.Add(combo.Id))
            {
                throw new ArgumentException($"Id de combo repetido: '{combo.Id}'.", nameof(combos));
            }
        }
    }

    /// <summary>Feeds one reading of controller <paramref name="controllerIndex"/> (0 to 3).
    /// Returns the combo that fired on this reading, or null. At most one combo can fire
    /// per reading, because a combo only counts when the buttons match it exactly and no
    /// two combos share the same buttons.</summary>
    public ComboDefinition? Update(int controllerIndex, GamepadButtons pressed, TimeSpan timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(controllerIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(controllerIndex, MaxControllers);

        GamepadButtons mask = pressed & GamepadButtonMask.AllDigital;
        ref ControllerState state = ref _states[controllerIndex];

        switch (state.Phase)
        {
            case Phase.WaitingRelease:
                if ((mask & state.Combo!.Buttons) != GamepadButtons.None)
                {
                    return null;
                }

                state = default;
                break;

            case Phase.Holding:
                if (mask == state.Combo!.Buttons)
                {
                    return FireIfDue(ref state, timestamp);
                }

                // An extra button or a released one: the count is cancelled. The reading is
                // looked at again from Idle below, so it may start another combo's count.
                state = default;
                break;
        }

        foreach (var combo in _combos)
        {
            if (combo.Buttons == mask)
            {
                state = new ControllerState(Phase.Holding, combo, timestamp);
                return FireIfDue(ref state, timestamp);
            }
        }

        return null;
    }

    /// <summary>Controller <paramref name="controllerIndex"/> went away: whatever it was
    /// counting is cancelled and its state starts over.</summary>
    public void Disconnect(int controllerIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(controllerIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(controllerIndex, MaxControllers);

        _states[controllerIndex] = default;
    }

    /// <summary>Forgets every controller's state (the session ended).</summary>
    public void Reset() => Array.Clear(_states);

    private static ComboDefinition? FireIfDue(ref ControllerState state, TimeSpan timestamp)
    {
        if (timestamp - state.Since < state.Combo!.Hold)
        {
            return null;
        }

        var fired = state.Combo;
        state = new ControllerState(Phase.WaitingRelease, fired, TimeSpan.Zero);
        return fired;
    }

    private enum Phase
    {
        Idle,
        Holding,
        WaitingRelease,
    }

    private readonly record struct ControllerState(Phase Phase, ComboDefinition? Combo, TimeSpan Since);
}
