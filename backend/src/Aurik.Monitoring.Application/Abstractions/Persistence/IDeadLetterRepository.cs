using Aurik.Monitoring.Domain.Entities;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface IDeadLetterRepository
{
    Task InsertAsync(DeadLetterEntry entry, CancellationToken ct);
    Task<IReadOnlyList<DeadLetterEntry>> GetRecentAsync(int limit, CancellationToken ct);
}
