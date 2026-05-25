using Aurik.Monitoring.Domain.Entities;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface INormalizedEventRepository
{
    /// <summary>
    /// Inserts a batch idempotently, skipping events whose (vendor, vendor_event_id) already exist.
    /// Returns the number of newly inserted events.
    /// </summary>
    Task<int> InsertManyIdempotentAsync(IReadOnlyList<NormalizedEvent> events, CancellationToken ct);

    /// <summary>Latest events for a single machine, newest first.</summary>
    Task<IReadOnlyList<NormalizedEvent>> GetLatestForMachineAsync(string machineId, int limit, CancellationToken ct);

    Task<IReadOnlyList<NormalizedEvent>> GetSinceAsync(string machineId, DateTime since, CancellationToken ct);
}
