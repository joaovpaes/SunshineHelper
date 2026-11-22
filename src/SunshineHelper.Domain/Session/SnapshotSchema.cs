namespace SunshineHelper.Domain.Session;

/// <summary>Version of the session snapshot envelope written to disk. Bumped only when
/// the envelope itself changes; each module versions its own payload separately
/// (<see cref="ModuleSnapshot.PayloadVersion"/>), so a module can change what it stores
/// without forcing every other module to care.</summary>
public static class SnapshotSchema
{
    public const int CurrentVersion = 1;
}
