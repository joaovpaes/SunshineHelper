using Windows.Win32;
using Windows.Win32.Foundation;

namespace SunshineHelper.Infrastructure.Overlay;

/// <summary>
/// The two focus moves of the overlay (docs/adr/0015-overlay-pelo-controle.md, "Janela do
/// overlay"): remember which window is in front, and ask Windows to bring a window to the
/// front. Windows may refuse the second one for a program that is running in the background;
/// the overlay then works anyway and the buttons may also reach the game (risk R8).
/// </summary>
public static class WindowsForegroundFocus
{
    /// <summary>The window in front right now, or 0 when there is none.</summary>
    public static nint GetForeground() => PInvoke.GetForegroundWindow().Value;

    /// <summary>Asks Windows to bring <paramref name="window"/> to the front. False when
    /// there is no window or Windows refused.</summary>
    public static bool TryFocus(nint window) => window != 0 && PInvoke.SetForegroundWindow(new HWND(window));
}
