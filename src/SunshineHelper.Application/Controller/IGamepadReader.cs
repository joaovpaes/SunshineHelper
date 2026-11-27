using SunshineHelper.Domain.Gamepad;

namespace SunshineHelper.Application.Controller;

/// <summary>What one controller slot reported on one reading.</summary>
public readonly record struct GamepadSample(bool Connected, GamepadButtons Buttons);

/// <summary>
/// Reads the digital buttons of the (up to four) controllers Windows shows as Xbox-style
/// (docs/adr/0015-overlay-pelo-controle.md). The real one asks XInput; tests use a script.
/// An implementation never throws: a controller it cannot read is "not connected".
/// </summary>
public interface IGamepadReader
{
    /// <param name="index">Controller slot, 0 to 3.</param>
    GamepadSample Read(int index);
}
