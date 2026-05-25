namespace Aurik.Monitoring.Application.Processing;

public interface IPayloadProcessor
{
    /// <summary>
    /// Process one raw payload end-to-end:
    /// load → normalize → persist normalized events idempotently → recompute affected machine views.
    /// </summary>
    Task<ProcessingOutcome> ProcessAsync(string rawPayloadId, int attemptNumber, CancellationToken ct);
}

public sealed record ProcessingOutcome(
    bool Success,
    int NormalizedCount,
    int IssueCount,
    string? FailureReason,
    bool Retryable);
