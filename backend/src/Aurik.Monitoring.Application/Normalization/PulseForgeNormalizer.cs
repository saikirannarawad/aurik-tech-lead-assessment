using System.Globalization;
using System.Text.Json;
using Aurik.Monitoring.Application.Vendors.PulseForge;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Normalization;

public sealed class PulseForgeNormalizer : INormalizer
{
    public VendorType Vendor => VendorType.PulseForge;

    public NormalizationResult Normalize(string rawJson, string rawPayloadId)
    {
        var payload = JsonSerializer.Deserialize<PulseForgePayload>(rawJson)
                      ?? throw new InvalidDataException("Unable to deserialize PulseForge payload.");

        var events = new List<NormalizedEvent>();
        var issues = new List<NormalizationIssue>();
        var now = DateTime.UtcNow;

        for (var i = 0; i < payload.Events.Count; i++)
        {
            var src = payload.Events[i];
            var locator = $"events[{i}]";

            if (string.IsNullOrWhiteSpace(src.EventId))
            {
                issues.Add(new NormalizationIssue(locator, "missing event_id"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(src.MachineId))
            {
                issues.Add(new NormalizationIssue(locator, "missing machine_id"));
                continue;
            }
            if (!TryParseIsoUtc(src.EventTime, out var eventTime))
            {
                issues.Add(new NormalizationIssue(locator, $"invalid event_time '{src.EventTime}'"));
                continue;
            }

            events.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                RawPayloadId = rawPayloadId,
                Vendor = VendorType.PulseForge,
                VendorEventId = src.EventId!,
                IdempotencyKey = $"{VendorType.PulseForge}:{src.EventId}",
                MachineId = src.MachineId!,
                PlantId = payload.PlantId ?? string.Empty,
                LineId = NormalizeLineId(src.LineId),
                EventTimeUtc = eventTime,
                ProcessedAtUtc = now,
                EventType = MapEventType(src.EventType),
                SeverityHint = MapSeverity(src.Severity),
                VendorEventCode = src.EventType,
                VibrationMmPerSec = src.VibrationMmS,
                TemperatureCelsius = src.TemperatureC,
                PowerKw = null,
                SensorHealth = src.SensorHealth,
                VendorConfidence = src.VendorConfidence,
                MachineState = src.MachineState
            });
        }

        return new NormalizationResult(events, issues);
    }

    private static bool TryParseIsoUtc(string? value, out DateTime utc)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out utc))
            return true;
        utc = default;
        return false;
    }

    private static string NormalizeLineId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        // PulseForge already sends "LINE-A"; if a short form ever sneaks in, normalize it.
        return raw.StartsWith("LINE-", StringComparison.OrdinalIgnoreCase) ? raw.ToUpperInvariant() : $"LINE-{raw.ToUpperInvariant()}";
    }

    private static CanonicalEventType MapEventType(string? vendorCode) => vendorCode?.ToUpperInvariant() switch
    {
        "HIGH_VIBRATION" => CanonicalEventType.HighVibration,
        "TEMP_SPIKE" => CanonicalEventType.HighTemperature,
        "SENSOR_HEALTH_DROP" => CanonicalEventType.SensorHealthDegraded,
        "POWER_FLUCTUATION" => CanonicalEventType.PowerAnomaly,
        "RECOVERY_SIGNAL" => CanonicalEventType.Recovery,
        _ => CanonicalEventType.Unknown
    };

    private static AttentionLevel MapSeverity(string? severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => AttentionLevel.Critical,
        "high" => AttentionLevel.High,
        "medium" => AttentionLevel.Moderate,
        "low" => AttentionLevel.Low,
        _ => AttentionLevel.None
    };
}
