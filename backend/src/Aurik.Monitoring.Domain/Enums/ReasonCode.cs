namespace Aurik.Monitoring.Domain.Enums;

/// <summary>
/// Explainable reason codes attached to a machine's derived attention view.
/// New codes can be added without breaking consumers.
/// </summary>
public static class ReasonCode
{
    public const string VibrationOverThreshold = "VIBRATION_OVER_THRESHOLD";
    public const string TemperatureOverThreshold = "TEMPERATURE_OVER_THRESHOLD";
    public const string PowerAnomaly = "POWER_ANOMALY";
    public const string SensorHealthLow = "SENSOR_HEALTH_LOW";
    public const string VendorReportedCritical = "VENDOR_REPORTED_CRITICAL";
    public const string VendorReportedHigh = "VENDOR_REPORTED_HIGH";
    public const string MaintenanceOverdue = "MAINTENANCE_OVERDUE";
    public const string MaintenanceDueSoon = "MAINTENANCE_DUE_SOON";
    public const string InspectionDefect = "INSPECTION_DEFECT";
    public const string OperatorConcern = "OPERATOR_CONCERN";
    public const string StaleSignal = "STALE_SIGNAL";
    public const string RecoveryObserved = "RECOVERY_OBSERVED";
}
