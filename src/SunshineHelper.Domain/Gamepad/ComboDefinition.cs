namespace SunshineHelper.Domain.Gamepad;

/// <summary>One combo the <see cref="ComboDetector"/> watches for: an exact set of
/// buttons held together for <see cref="Hold"/>. <see cref="Id"/> tells the caller which
/// action to run ("closeGame", "overlay").</summary>
public sealed record ComboDefinition(string Id, GamepadButtons Buttons, TimeSpan Hold);
