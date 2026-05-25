using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface IMachineViewRepository
{
    Task<MachineOperationalView?> GetAsync(string machineId, CancellationToken ct);

    /// <summary>
    /// Optimistic-concurrency upsert: write succeeds only if stored Version == expectedVersion (or doc absent).
    /// Returns true on success, false on concurrency conflict (caller should re-read and retry).
    /// </summary>
    Task<bool> UpsertWithVersionAsync(MachineOperationalView view, long expectedVersion, CancellationToken ct);

    Task<IReadOnlyList<MachineOperationalView>> QueryAsync(
        string? plantId,
        string? lineId,
        DerivedStatus? status,
        AttentionLevel? minAttention,
        int limit,
        CancellationToken ct);
}
