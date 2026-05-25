using System.Text.Json.Serialization;

namespace Aurik.Monitoring.Application.Vendors.PulseForge;

public sealed class PulseForgePayload
{
    [JsonPropertyName("vendor")] public string? Vendor { get; set; }
    [JsonPropertyName("plant_id")] public string? PlantId { get; set; }
    [JsonPropertyName("batch_generated_at")] public string? BatchGeneratedAt { get; set; }
    [JsonPropertyName("events")] public List<PulseForgeEvent> Events { get; set; } = new();
}

public sealed class PulseForgeEvent
{
    [JsonPropertyName("event_id")] public string? EventId { get; set; }
    [JsonPropertyName("machine_id")] public string? MachineId { get; set; }
    [JsonPropertyName("line_id")] public string? LineId { get; set; }
    [JsonPropertyName("event_time")] public string? EventTime { get; set; }
    [JsonPropertyName("event_type")] public string? EventType { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
    [JsonPropertyName("vibration_mm_s")] public double? VibrationMmS { get; set; }
    [JsonPropertyName("temperature_c")] public double? TemperatureC { get; set; }
    [JsonPropertyName("machine_state")] public string? MachineState { get; set; }
    [JsonPropertyName("sensor_health")] public double? SensorHealth { get; set; }
    [JsonPropertyName("vendor_confidence")] public double? VendorConfidence { get; set; }
}
