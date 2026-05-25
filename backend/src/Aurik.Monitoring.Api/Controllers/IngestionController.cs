using System.IO;
using System.Text;
using Aurik.Monitoring.Api.Middleware;
using Aurik.Monitoring.Application.Ingestion;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Aurik.Monitoring.Api.Controllers;

/// <summary>
/// Vendor ingestion endpoints. Auth is enforced by <see cref="ApiKeyAuthMiddleware"/>.
/// </summary>
[ApiController]
[Route("api/ingestion")]
public sealed class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestion;

    public IngestionController(IIngestionService ingestion) => _ingestion = ingestion;

    /// <summary>Ingest a PulseForge batch payload.</summary>
    [HttpPost("pulseforge")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> IngestPulseForge(CancellationToken ct) => IngestAsync(VendorType.PulseForge, ct);

    /// <summary>Ingest a ThermexWatch batch payload.</summary>
    [HttpPost("thermexwatch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> IngestThermexWatch(CancellationToken ct) => IngestAsync(VendorType.ThermexWatch, ct);

    /// <summary>Ingest a MaintaFlow batch payload.</summary>
    [HttpPost("maintaflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> IngestMaintaFlow(CancellationToken ct) => IngestAsync(VendorType.MaintaFlow, ct);

    private async Task<IActionResult> IngestAsync(VendorType vendor, CancellationToken ct)
    {
        // Read body verbatim so the raw JSON is preserved for traceability and replay.
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { error = "empty_body" });

        if (body.Length > 1_048_576) // 1 MB guardrail
            return BadRequest(new { error = "payload_too_large", limit_bytes = 1_048_576 });

        // Caller-supplied keys win; otherwise derive from content hash so identical bodies dedupe.
        var idempotencyKey = Request.Headers.TryGetValue("X-Idempotency-Key", out var k) && !string.IsNullOrWhiteSpace(k)
            ? k.ToString()
            : IdempotencyKey.ForPayload(vendor, body);

        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var ack = await _ingestion.AcceptAsync(vendor, body, idempotencyKey, sourceIp, ct).ConfigureAwait(false);

        var statusCode = ack.Duplicate ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;
        return StatusCode(statusCode, new
        {
            raw_payload_id = ack.RawPayloadId,
            state = ack.State.ToString(),
            duplicate = ack.Duplicate,
            record_count = ack.RecordCount,
            idempotency_key = idempotencyKey
        });
    }
}
