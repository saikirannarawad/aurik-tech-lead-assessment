using System.Text.Json.Serialization;

namespace Aurik.Monitoring.Application.Vendors.MaintaFlow;

public sealed class MaintaFlowPayload
{
    [JsonPropertyName("provider_name")] public string? ProviderName { get; set; }
    [JsonPropertyName("factory_id")] public string? FactoryId { get; set; }
    [JsonPropertyName("records")] public List<MaintaFlowRecord> Records { get; set; } = new();
}

public sealed class MaintaFlowRecord
{
    [JsonPropertyName("record_id")] public string? RecordId { get; set; }
    [JsonPropertyName("machine_ref")] public string? MachineRef { get; set; }
    [JsonPropertyName("line_ref")] public string? LineRef { get; set; }
    [JsonPropertyName("recorded_at")] public string? RecordedAt { get; set; }
    [JsonPropertyName("record_type")] public string? RecordType { get; set; }
    [JsonPropertyName("inspection_result")] public string? InspectionResult { get; set; }
    [JsonPropertyName("maintenance_status")] public string? MaintenanceStatus { get; set; }
    [JsonPropertyName("days_since_last_service")] public int? DaysSinceLastService { get; set; }
    [JsonPropertyName("technician_note")] public string? TechnicianNote { get; set; }
    [JsonPropertyName("manual_confidence")] public string? ManualConfidence { get; set; }
}
