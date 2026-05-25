using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Aurik.Monitoring.Api.Controllers;

[ApiController]
[Route("api/machines")]
public sealed class MachinesController : ControllerBase
{
    private readonly IMachineViewRepository _viewRepo;
    private readonly IMachineRepository _machineRepo;
    private readonly INormalizedEventRepository _eventRepo;

    public MachinesController(
        IMachineViewRepository viewRepo,
        IMachineRepository machineRepo,
        INormalizedEventRepository eventRepo)
    {
        _viewRepo = viewRepo;
        _machineRepo = machineRepo;
        _eventRepo = eventRepo;
    }

    /// <summary>List machine operational views with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? plantId,
        [FromQuery] string? lineId,
        [FromQuery] DerivedStatus? status,
        [FromQuery] AttentionLevel? minAttention,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var views = await _viewRepo.QueryAsync(plantId, lineId, status, minAttention, limit, ct).ConfigureAwait(false);
        return Ok(views.Select(ToDto));
    }

    /// <summary>Per-machine operational view: derived status, attention, reason codes, refs.</summary>
    [HttpGet("{machineId}/view")]
    public async Task<IActionResult> GetView(string machineId, CancellationToken ct)
    {
        var view = await _viewRepo.GetAsync(machineId, ct).ConfigureAwait(false);
        if (view is null) return NotFound(new { error = "not_found", machine_id = machineId });
        return Ok(ToDto(view));
    }

    /// <summary>The most recent normalized events for a machine, newest first.</summary>
    [HttpGet("{machineId}/events")]
    public async Task<IActionResult> GetEvents(string machineId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var events = await _eventRepo.GetLatestForMachineAsync(machineId, limit, ct).ConfigureAwait(false);
        return Ok(events.Select(e => new
        {
            id = e.Id,
            vendor = e.Vendor.ToString(),
            vendor_event_id = e.VendorEventId,
            vendor_event_code = e.VendorEventCode,
            canonical_type = e.EventType.ToString(),
            severity_hint = e.SeverityHint.ToString(),
            event_time = e.EventTimeUtc,
            vibration_mm_s = e.VibrationMmPerSec,
            temperature_c = e.TemperatureCelsius,
            power_kw = e.PowerKw,
            sensor_health = e.SensorHealth,
            note = e.Note,
            maintenance_status = e.MaintenanceStatus,
            inspection_result = e.InspectionResult
        }));
    }

    private static object ToDto(Aurik.Monitoring.Domain.Entities.MachineOperationalView v) => new
    {
        machine_id = v.MachineId,
        plant_id = v.PlantId,
        line_id = v.LineId,
        derived_status = v.DerivedStatus.ToString(),
        attention_level = v.AttentionLevel.ToString(),
        needs_attention = v.NeedsAttention,
        reason_codes = v.ReasonCodes,
        latest_relevant_event_time = v.LatestRelevantEventTime,
        processing_status = v.ProcessingStatus.ToString(),
        source_event_refs = v.SourceEventRefs,
        last_processed_at = v.LastProcessedAt,
        version = v.Version
    };
}
