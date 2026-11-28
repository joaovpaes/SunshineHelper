namespace SunshineHelper.Application.Controller;

/// <summary>The entries of the overlay's home screen, in the order of the approved prototype.</summary>
public enum OverlayItem
{
    Profiles,
    Adjust,
    Windows,
    Shortcuts,

    /// <summary>Put the screen back to how it was before the stream, now, with the stream going on.</summary>
    Restore,
}

/// <summary>What the overlay's window must do after a press.</summary>
public enum OverlayRequest
{
    None,

    /// <summary>Close the overlay.</summary>
    Close,

    /// <summary>Restore the environment now (the user confirmed).</summary>
    RestoreNow,
}

/// <summary>
/// The home screen of the overlay as a state machine (docs/adr/0015-overlay-pelo-controle.md,
/// PR F): which row is selected, and the confirmation of "Restore now". Pure, so every press is
/// tested without a window. The other screens (profiles, adjustments, windows, shortcuts) are
/// listed but not available yet; they arrive in later PRs, and the selection skips them.
/// </summary>
public sealed class OverlayModel
{
    private static readonly OverlayItem[] _all =
    [
        OverlayItem.Profiles,
        OverlayItem.Adjust,
        OverlayItem.Windows,
        OverlayItem.Shortcuts,
        OverlayItem.Restore,
    ];

    public OverlayModel()
    {
        Selected = FirstAvailable();
    }

    /// <summary>Every entry, in screen order.</summary>
    public IReadOnlyList<OverlayItem> Items => _all;

    /// <summary>The selected entry, or null when nothing can be selected.</summary>
    public OverlayItem? Selected { get; private set; }

    /// <summary>True while the "Restore now?" question is on screen.</summary>
    public bool Confirming { get; private set; }

    /// <summary>True once the environment was restored from here: there is nothing left to
    /// restore, and the stream goes on without the profile.</summary>
    public bool Restored { get; private set; }

    /// <summary>Whether an entry works in this version. Restore only works while there is
    /// something to restore.</summary>
    public bool IsAvailable(OverlayItem item) => item == OverlayItem.Restore && !Restored;

    /// <summary>Applies one press and says what the window has to do about it.</summary>
    public OverlayRequest Handle(NavInput input)
    {
        if (Confirming)
        {
            switch (input)
            {
                case NavInput.Confirm:
                    Confirming = false;
                    return OverlayRequest.RestoreNow;
                case NavInput.Back:
                    Confirming = false;
                    return OverlayRequest.None;
                default:
                    return OverlayRequest.None;
            }
        }

        switch (input)
        {
            case NavInput.Up:
                Move(-1);
                return OverlayRequest.None;
            case NavInput.Down:
                Move(1);
                return OverlayRequest.None;
            case NavInput.Back:
                return OverlayRequest.Close;
            case NavInput.Confirm when Selected == OverlayItem.Restore:
                Confirming = true;
                return OverlayRequest.None;
            default:
                return OverlayRequest.None;
        }
    }

    /// <summary>Called by the window once the restore finished.</summary>
    public void MarkRestored()
    {
        Restored = true;
        Confirming = false;
        Selected = FirstAvailable();
    }

    private OverlayItem? FirstAvailable()
    {
        foreach (var item in _all)
        {
            if (IsAvailable(item))
            {
                return item;
            }
        }

        return null;
    }

    private void Move(int step)
    {
        if (Selected is not { } current)
        {
            return;
        }

        int index = Array.IndexOf(_all, current);
        for (int next = index + step; next >= 0 && next < _all.Length; next += step)
        {
            if (IsAvailable(_all[next]))
            {
                Selected = _all[next];
                return;
            }
        }
    }
}
