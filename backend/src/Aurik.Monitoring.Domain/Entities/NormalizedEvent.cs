using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Domain.Entities;

/// <summary>
/// Canonical internal representation of a vendor event after normalization.
/// Field mapping decisions are deliberate; null means "vendor did not provide this signal".
/// </summary>
public sealed class NormalizedEvent
{
    public required string Id { get; init; }
    public required string RawPayloadId { get; init; }
    public required VendorType Vendor { get; init; }
    public required string VendorEventId { get; init; }
    public required string IdempotencyKey { get; init; }

    public required string MachineId { get; init; }
    public required string PlantId { get; init; }
    public required string LineId { get; init; }

    public required DateTime EventTimeUtc { get; init; }
    public required DateTime ProcessedAtUtc { get; init; }

    public required CanonicalEventType EventType { get; init; }
    public required AttentionLevel SeverityHint { get; init; }
    public string? VendorEventCode { get; init; }

    public double? VibrationMmPerSec { get; init; }
    public double? TemperatureCelsius { get; init; }
    public double? PowerKw { get; init; }
    public double? SensorHealth { get; init; }
    public double? VendorConfidence { get; init; }
    public string? MachineState { get; init; }
    public string? Note { get; init; }
    public int? DaysSinceLastService { get; init; }
    public string? MaintenanceStatus { get; init; }
    public string? InspectionResult { get; init; }
}
