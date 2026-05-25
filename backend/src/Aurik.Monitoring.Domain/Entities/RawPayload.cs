using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Domain.Entities;

/// <summary>
/// Raw vendor payload as received. Preserved verbatim for traceability and replay.
/// </summary>
public sealed class RawPayload
{
    public required string Id { get; init; }
    public required VendorType Vendor { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string RawJson { get; init; }
    public required DateTime ReceivedAt { get; init; }
    public required string SourceIp { get; init; }
    public int RecordCount { get; init; }
    public ProcessingState State { get; set; } = ProcessingState.Accepted;
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}
