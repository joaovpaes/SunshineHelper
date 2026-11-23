namespace SunshineHelper.Application.Monitoring;

/// <summary>
/// Which Sunshine processes are running right now, by process id. Infrastructure lists
/// them by name without opening them (Sunshine runs as SYSTEM, the app does not); tests
/// say whatever they need.
/// </summary>
public interface ISunshineProcessProbe
{
    IReadOnlyCollection<int> FindProcessIds();
}
