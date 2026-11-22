namespace SunshineHelper.Domain.Settings;

/// <summary>
/// The time budget measured in Phase 0 (docs/fase0-resultados.md) and fixed in
/// docs/adr/0002-arquitetura-do-nucleo.md, "Orçamento de tempo". Defaults match the ADR.
/// </summary>
public sealed record TimingSettings
{
    public int ApplyTargetMs { get; init; } = 10_000;

    public int BarrierDefaultMs { get; init; } = 20_000;

    public int BarrierCeilingMs { get; init; } = 55_000;

    /// <summary>Delay before releasing the launcher, used while Phase 1 measures
    /// whether the orchestrator's own safety net makes this unnecessary (ADR 0002,
    /// "Fechamento do launcher"). Zero disables it.</summary>
    public int LauncherDelayMs { get; init; }
}
