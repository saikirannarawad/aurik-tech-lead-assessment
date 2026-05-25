using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Aurik.Monitoring.Api.Controllers;

[ApiController]
[Route("api/plants")]
public sealed class PlantsController : ControllerBase
{
    private readonly IMachineViewRepository _viewRepo;
    private readonly IMachineRepository _machineRepo;
    private readonly ILineRepository _lineRepo;

    public PlantsController(
        IMachineViewRepository viewRepo,
        IMachineRepository machineRepo,
        ILineRepository lineRepo)
    {
        _viewRepo = viewRepo;
        _machineRepo = machineRepo;
        _lineRepo = lineRepo;
    }

    /// <summary>Plant-level operational summary: status counts, attention counts, lines breakdown.</summary>
    [HttpGet("{plantId}/summary")]
    public async Task<IActionResult> GetPlantSummary(string plantId, CancellationToken ct)
    {
        var views = await _viewRepo.QueryAsync(plantId, null, null, null, 1000, ct).ConfigureAwait(false);

        // Zero counters for every status so consumers get a stable shape even before any payloads arrive.
        var statusCounts = Enum.GetValues<DerivedStatus>()
            .ToDictionary(s => s.ToString(), s => views.Count(v => v.DerivedStatus == s));

        var byLine = views.GroupBy(v => v.LineId)
            .Select(g => new
            {
                line_id = g.Key,
                total = g.Count(),
                needing_attention = g.Count(v => v.NeedsAttention),
                critical = g.Count(v => v.DerivedStatus == DerivedStatus.Critical),
                stale = g.Count(v => v.DerivedStatus == DerivedStatus.Stale)
            })
            .OrderByDescending(x => x.needing_attention)
            .ToList();

        var critical = views.Where(v => v.DerivedStatus == DerivedStatus.Critical)
            .Select(v => new { machine_id = v.MachineId, line_id = v.LineId, reason_codes = v.ReasonCodes })
            .ToList();

        return Ok(new
        {
            plant_id = plantId,
            total_machines = views.Count,
            needing_attention = views.Count(v => v.NeedsAttention),
            status_counts = statusCounts,
            lines = byLine,
            critical_machines = critical,
            has_data = views.Count > 0
        });
    }

    /// <summary>Line-level summary scoped to a plant.</summary>
    [HttpGet("{plantId}/lines/{lineId}/summary")]
    public async Task<IActionResult> GetLineSummary(string plantId, string lineId, CancellationToken ct)
    {
        var views = await _viewRepo.QueryAsync(plantId, lineId, null, null, 1000, ct).ConfigureAwait(false);

        var statusCounts = Enum.GetValues<DerivedStatus>()
            .ToDictionary(s => s.ToString(), s => views.Count(v => v.DerivedStatus == s));

        return Ok(new
        {
            plant_id = plantId,
            line_id = lineId,
            total_machines = views.Count,
            needing_attention = views.Count(v => v.NeedsAttention),
            status_counts = statusCounts,
            machines = views.Select(v => new
            {
                machine_id = v.MachineId,
                derived_status = v.DerivedStatus.ToString(),
                attention_level = v.AttentionLevel.ToString(),
                needs_attention = v.NeedsAttention,
                reason_codes = v.ReasonCodes
            }),
            has_data = views.Count > 0
        });
    }

    /// <summary>List all plants and lines from reference data.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPlants(CancellationToken ct)
    {
        var machines = await _machineRepo.GetAllAsync(ct).ConfigureAwait(false);
        var lines = await _lineRepo.GetAllAsync(ct).ConfigureAwait(false);

        var plants = machines.GroupBy(m => m.PlantId).Select(g => new
        {
            plant_id = g.Key,
            machine_count = g.Count(),
            lines = lines.Where(l => l.PlantId == g.Key).Select(l => new
            {
                line_id = l.LineId,
                line_name = l.LineName,
                operating_window = l.OperatingWindow
            })
        });

        return Ok(plants);
    }
}
