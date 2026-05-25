using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface IRawPayloadRepository
{
    /// <summary>
    /// Inserts a payload if the idempotency key is unseen. Returns the persisted entity (existing or new)
    /// and a flag indicating whether the insert was new (true) or already present (false).
    /// </summary>
    Task<(RawPayload payload, bool isNew)> InsertIfNotExistsAsync(RawPayload payload, CancellationToken ct);

    Task<RawPayload?> GetAsync(string id, CancellationToken ct);

    Task UpdateStateAsync(string id, ProcessingState state, string? failureReason, int attemptCount, CancellationToken ct);

    Task<IReadOnlyList<RawPayload>> GetRecentAsync(int limit, CancellationToken ct);
}
