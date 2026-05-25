namespace Aurik.Monitoring.Domain.Enums;

/// <summary>
/// The derived operational status of a machine, computed from normalized vendor events.
/// </summary>
public enum DerivedStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    AtRisk = 3,
    Critical = 4,
    UnderMaintenance = 5,
    Stale = 6
}
