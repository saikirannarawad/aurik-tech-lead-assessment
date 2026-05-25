using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Domain.Entities;

/// <summary>
/// A payload that exhausted retries or hit a permanent error. Held for inspection / manual replay.
/// </summary>
public sealed class DeadLetterEntry
{
    public required string Id { get; init; }
    public required string RawPayloadId { get; init; }
    public required VendorType Vendor { get; init; }
    public required DateTime DeadLetteredAt { get; init; }
    public required string Reason { get; init; }
    public int AttemptCount { get; init; }
    public string? StackTrace { get; init; }
}
