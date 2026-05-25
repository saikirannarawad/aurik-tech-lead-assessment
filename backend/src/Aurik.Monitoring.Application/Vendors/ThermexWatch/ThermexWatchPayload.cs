using System.Text.Json.Serialization;

namespace Aurik.Monitoring.Application.Vendors.ThermexWatch;

public sealed class ThermexWatchPayload
{
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("site_code")] public string? SiteCode { get; set; }
    [JsonPropertyName("response_time_epoch_ms")] public long? ResponseTimeEpochMs { get; set; }
    [JsonPropertyName("readings")] public List<ThermexWatchReading> Readings { get; set; } = new();
}

public sealed class ThermexWatchReading
{
    [JsonPropertyName("readingId")] public string? ReadingId { get; set; }
    [JsonPropertyName("assetCode")] public string? AssetCode { get; set; }
    [JsonPropertyName("productionLine")] public string? ProductionLine { get; set; }
    [JsonPropertyName("timestampMs")] public long? TimestampMs { get; set; }
    [JsonPropertyName("alertCode")] public string? AlertCode { get; set; }
    [JsonPropertyName("level")] public int? Level { get; set; }
    [JsonPropertyName("vibration_g")] public double? VibrationG { get; set; }
    [JsonPropertyName("temperature_f")] public double? TemperatureF { get; set; }
    [JsonPropertyName("power_kw")] public double? PowerKw { get; set; }
    [JsonPropertyName("is_active")] public bool? IsActive { get; set; }
    [JsonPropertyName("signal_quality")] public string? SignalQuality { get; set; }
}
