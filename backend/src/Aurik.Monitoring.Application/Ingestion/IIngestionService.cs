using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Ingestion;

public interface IIngestionService
{
    /// <summary>
    /// Accept a vendor payload: persist raw bytes, dedupe by idempotency key, enqueue for async processing.
    /// </summary>
    Task<IngestionAck> AcceptAsync(VendorType vendor, string rawJson, string idempotencyKey, string sourceIp, CancellationToken ct);
}

public sealed record IngestionAck(
    string RawPayloadId,
    ProcessingState State,
    bool Duplicate,
    int RecordCount);
