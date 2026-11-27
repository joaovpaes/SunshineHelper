using SunshineHelper.Application.Controller;
using SunshineHelper.Domain.Gamepad;
using Windows.Win32;
using Windows.Win32.UI.Input.XboxController;

namespace SunshineHelper.Infrastructure.Controller;

/// <summary>
/// Reads Xbox-style controllers through XInput (<c>xinput1_4.dll</c>), the choice of
/// docs/adr/0015-overlay-pelo-controle.md. The virtual controller Sunshine creates for a
/// Moonlight client shows up here like a real one, as long as Sunshine's <c>gamepad</c>
/// option makes it an Xbox 360 pad (a PlayStation-style virtual pad is invisible to XInput;
/// the guide explains the setting).
/// </summary>
/// <remarks>
/// The Guide button is not read (it needs an undocumented call) and the triggers and sticks
/// are analog, so only the digital button bits reach the detector. Never throws: a slot that
/// XInput cannot answer for, or a system without the DLL, is "not connected".
/// </remarks>
public sealed class XInputGamepadReader : IGamepadReader
{
    private const uint Success = 0;

    public GamepadSample Read(int index)
    {
        if (index is < 0 or >= ComboDetector.MaxControllers)
        {
            return default;
        }

        try
        {
            uint result = PInvoke.XInputGetState((uint)index, out XINPUT_STATE state);
            if (result != Success)
            {
                return default;
            }

            var buttons = (GamepadButtons)(uint)state.Gamepad.wButtons & GamepadButtonMask.AllDigital;
            return new GamepadSample(true, buttons);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return default;
        }
    }
}
