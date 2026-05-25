using System.Text.Json;
using Aurik.Monitoring.Application.Vendors.ThermexWatch;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using Aurik.Monitoring.Domain.ValueObjects;

namespace Aurik.Monitoring.Application.Normalization;

public sealed class ThermexWatchNormalizer : INormalizer
{
    public VendorType Vendor => VendorType.ThermexWatch;

    public NormalizationResult Normalize(string rawJson, string rawPayloadId)
    {
        var payload = JsonSerializer.Deserialize<ThermexWatchPayload>(rawJson)
                      ?? throw new InvalidDataException("Unable to deserialize ThermexWatch payload.");

        var events = new List<NormalizedEvent>();
        var issues = new List<NormalizationIssue>();
        var now = DateTime.UtcNow;

        for (var i = 0; i < payload.Readings.Count; i++)
        {
            var src = payload.Readings[i];
            var locator = $"readings[{i}]";

            if (string.IsNullOrWhiteSpace(src.ReadingId))
            {
                issues.Add(new NormalizationIssue(locator, "missing readingId"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(src.AssetCode))
            {
                issues.Add(new NormalizationIssue(locator, "missing assetCode"));
                continue;
            }
            if (src.TimestampMs is null or <= 0)
            {
                issues.Add(new NormalizationIssue(locator, "missing/invalid timestampMs"));
                continue;
            }

            events.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                RawPayloadId = rawPayloadId,
                Vendor = VendorType.ThermexWatch,
                VendorEventId = src.ReadingId!,
                IdempotencyKey = $"{VendorType.ThermexWatch}:{src.ReadingId}",
                MachineId = src.AssetCode!,
                PlantId = payload.SiteCode ?? string.Empty,
                LineId = NormalizeLineId(src.ProductionLine),
                EventTimeUtc = UnitConversions.EpochMillisToUtc(src.TimestampMs!.Value),
                ProcessedAtUtc = now,
                EventType = MapAlertCode(src.AlertCode),
                SeverityHint = MapLevel(src.Level),
                VendorEventCode = src.AlertCode,
                VibrationMmPerSec = src.VibrationG.HasValue
                    ? UnitConversions.GravityToMmPerSec(src.VibrationG.Value)
                    : null,
                TemperatureCelsius = src.TemperatureF.HasValue
                    ? UnitConversions.FahrenheitToCelsius(src.TemperatureF.Value)
                    : null,
                PowerKw = src.PowerKw,
                SensorHealth = MapSignalQualityToConfidence(src.SignalQuality),
                VendorConfidence = MapSignalQualityToConfidence(src.SignalQuality),
                MachineState = src.IsActive == true ? "running" : "idle"
            });
        }

        return new NormalizationResult(events, issues);
    }

    private static string NormalizeLineId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        // ThermexWatch sends "A", "B" — normalize to canonical "LINE-A".
        return raw.StartsWith("LINE-", StringComparison.OrdinalIgnoreCase) ? raw.ToUpperInvariant() : $"LINE-{raw.ToUpperInvariant()}";
    }

    private static CanonicalEventType MapAlertCode(string? code) => code?.ToUpperInvariant() switch
    {
        "VIB_WARN" => CanonicalEventType.HighVibration,
        "TEMP_WARN" => CanonicalEventType.HighTemperature,
        "TEMP_CRIT" => CanonicalEventType.HighTemperature,
        "POWER_DROP" => CanonicalEventType.PowerAnomaly,
        "OK" => CanonicalEventType.NominalSignal,
        _ => CanonicalEventType.Unknown
    };

    private static AttentionLevel MapLevel(int? level) => level switch
    {
        5 => AttentionLevel.Critical,
        4 => AttentionLevel.High,
        3 => AttentionLevel.Moderate,
        2 => AttentionLevel.Low,
        1 => AttentionLevel.None,
        _ => AttentionLevel.None
    };

    private static double? MapSignalQualityToConfidence(string? quality) => quality?.ToUpperInvariant() switch
    {
        "GOOD" => 0.95,
        "FAIR" => 0.75,
        "POOR" => 0.40,
        _ => null
    };
}
