using System.Globalization;
using System.Text.Json;
using Aurik.Monitoring.Application.Vendors.MaintaFlow;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Normalization;

public sealed class MaintaFlowNormalizer : INormalizer
{
    public VendorType Vendor => VendorType.MaintaFlow;

    /// <summary>MaintaFlow uses "yyyy/MM/dd HH:mm:ss" (no timezone) — treat as UTC.</summary>
    private static readonly string[] MaintaFlowDateFormats = { "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd H:mm:ss" };

    public NormalizationResult Normalize(string rawJson, string rawPayloadId)
    {
        var payload = JsonSerializer.Deserialize<MaintaFlowPayload>(rawJson)
                      ?? throw new InvalidDataException("Unable to deserialize MaintaFlow payload.");

        var events = new List<NormalizedEvent>();
        var issues = new List<NormalizationIssue>();
        var now = DateTime.UtcNow;

        for (var i = 0; i < payload.Records.Count; i++)
        {
            var src = payload.Records[i];
            var locator = $"records[{i}]";

            if (string.IsNullOrWhiteSpace(src.RecordId))
            {
                issues.Add(new NormalizationIssue(locator, "missing record_id"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(src.MachineRef))
            {
                issues.Add(new NormalizationIssue(locator, "missing machine_ref"));
                continue;
            }
            if (!TryParseRecordedAt(src.RecordedAt, out var eventTime))
            {
                issues.Add(new NormalizationIssue(locator, $"invalid recorded_at '{src.RecordedAt}'"));
                continue;
            }

            events.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                RawPayloadId = rawPayloadId,
                Vendor = VendorType.MaintaFlow,
                VendorEventId = src.RecordId!,
                IdempotencyKey = $"{VendorType.MaintaFlow}:{src.RecordId}",
                MachineId = src.MachineRef!,
                PlantId = payload.FactoryId ?? string.Empty,
                LineId = NormalizeLineId(src.LineRef),
                EventTimeUtc = eventTime,
                ProcessedAtUtc = now,
                EventType = MapRecordType(src.RecordType),
                SeverityHint = DeriveSeverityHint(src),
                VendorEventCode = src.RecordType,
                VibrationMmPerSec = null,
                TemperatureCelsius = null,
                PowerKw = null,
                SensorHealth = null,
                VendorConfidence = MapManualConfidence(src.ManualConfidence),
                MachineState = null,
                Note = src.TechnicianNote,
                DaysSinceLastService = src.DaysSinceLastService,
                MaintenanceStatus = src.MaintenanceStatus,
                InspectionResult = src.InspectionResult
            });
        }

        return new NormalizationResult(events, issues);
    }

    private static bool TryParseRecordedAt(string? value, out DateTime utc)
    {
        if (DateTime.TryParseExact(
                value,
                MaintaFlowDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out utc))
            return true;

        // Fallback: try ISO 8601 if the vendor changes format.
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out utc);
    }

    private static string NormalizeLineId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.StartsWith("LINE-", StringComparison.OrdinalIgnoreCase) ? raw.ToUpperInvariant() : $"LINE-{raw.ToUpperInvariant()}";
    }

    private static CanonicalEventType MapRecordType(string? recordType) => recordType?.ToLowerInvariant() switch
    {
        "inspection" => CanonicalEventType.Inspection,
        "maintenance_update" => CanonicalEventType.MaintenanceUpdate,
        "operator_note" => CanonicalEventType.OperatorNote,
        "calibration" => CanonicalEventType.Calibration,
        _ => CanonicalEventType.Unknown
    };

    private static AttentionLevel DeriveSeverityHint(MaintaFlowRecord r)
    {
        // Maintenance/inspection severity isn't given directly — derive deterministically from fields.
        if (string.Equals(r.MaintenanceStatus, "overdue", StringComparison.OrdinalIgnoreCase))
            return AttentionLevel.High;
        if (string.Equals(r.InspectionResult, "minor_defect_found", StringComparison.OrdinalIgnoreCase))
            return AttentionLevel.Moderate;
        if (string.Equals(r.MaintenanceStatus, "due_soon", StringComparison.OrdinalIgnoreCase))
            return AttentionLevel.Low;
        return AttentionLevel.None;
    }

    private static double? MapManualConfidence(string? value) => value?.ToLowerInvariant() switch
    {
        "high" => 0.9,
        "medium" => 0.7,
        "low" => 0.4,
        _ => null
    };
}
