namespace Aurik.Monitoring.Domain.Enums;

/// <summary>
/// Canonical event taxonomy. Vendor-specific event codes are mapped here during normalization.
/// </summary>
public enum CanonicalEventType
{
    Unknown = 0,
    HighVibration = 1,
    HighTemperature = 2,
    PowerAnomaly = 3,
    SensorHealthDegraded = 4,
    Recovery = 5,
    Inspection = 6,
    MaintenanceUpdate = 7,
    OperatorNote = 8,
    Calibration = 9,
    NominalSignal = 10
}
