namespace SunshineHelper.Domain.Settings;

/// <summary>
/// One profile setting: either follow the client's own request
/// (<see cref="ProfileValueMode.FollowClient"/>), force <see cref="FixedValue"/>
/// (<see cref="ProfileValueMode.Fixed"/>), or leave the current value untouched
/// (<see cref="ProfileValueMode.Leave"/>).
/// </summary>
public sealed record ProfileValue<T>
{
    public required ProfileValueMode Mode { get; init; }

    /// <summary>Only meaningful when <see cref="Mode"/> is <see cref="ProfileValueMode.Fixed"/>.</summary>
    public T? FixedValue { get; init; }
}
