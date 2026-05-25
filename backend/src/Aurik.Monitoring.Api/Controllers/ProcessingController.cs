using Aurik.Monitoring.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Aurik.Monitoring.Api.Controllers;

[ApiController]
[Route("api/processing")]
public sealed class ProcessingController : ControllerBase
{
    private readonly IRawPayloadRepository _rawRepo;
    private readonly IDeadLetterRepository _dlqRepo;

    public ProcessingController(IRawPayloadRepository rawRepo, IDeadLetterRepository dlqRepo)
    {
        _rawRepo = rawRepo;
        _dlqRepo = dlqRepo;
    }

    /// <summary>Status of a specific accepted payload by raw_payload_id.</summary>
    [HttpGet("status/{rawPayloadId}")]
    public async Task<IActionResult> GetStatus(string rawPayloadId, CancellationToken ct)
    {
        var doc = await _rawRepo.GetAsync(rawPayloadId, ct).ConfigureAwait(false);
        if (doc is null) return NotFound(new { error = "not_found", raw_payload_id = rawPayloadId });

        return Ok(new
        {
            raw_payload_id = doc.Id,
            vendor = doc.Vendor.ToString(),
            state = doc.State.ToString(),
            received_at = doc.ReceivedAt,
            last_attempt_at = doc.LastAttemptAt,
            attempt_count = doc.AttemptCount,
            failure_reason = doc.FailureReason,
            record_count = doc.RecordCount,
            idempotency_key = doc.IdempotencyKey
        });
    }

    /// <summary>Most recent payload ingestions across all vendors.</summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var docs = await _rawRepo.GetRecentAsync(limit, ct).ConfigureAwait(false);
        return Ok(docs.Select(d => new
        {
            raw_payload_id = d.Id,
            vendor = d.Vendor.ToString(),
            state = d.State.ToString(),
            received_at = d.ReceivedAt,
            last_attempt_at = d.LastAttemptAt,
            attempt_count = d.AttemptCount,
            failure_reason = d.FailureReason,
            record_count = d.RecordCount
        }));
    }

    /// <summary>Dead-letter queue entries for inspection.</summary>
    [HttpGet("dead-letters")]
    public async Task<IActionResult> GetDeadLetters([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var docs = await _dlqRepo.GetRecentAsync(limit, ct).ConfigureAwait(false);
        return Ok(docs.Select(d => new
        {
            id = d.Id,
            raw_payload_id = d.RawPayloadId,
            vendor = d.Vendor.ToString(),
            dead_lettered_at = d.DeadLetteredAt,
            attempt_count = d.AttemptCount,
            reason = d.Reason
        }));
    }
}
