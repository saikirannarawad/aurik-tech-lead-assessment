using Aurik.Monitoring.Domain.Entities;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface ILineRepository
{
    Task<IReadOnlyList<Line>> GetAllAsync(CancellationToken ct);
    Task UpsertManyAsync(IReadOnlyList<Line> lines, CancellationToken ct);
}
