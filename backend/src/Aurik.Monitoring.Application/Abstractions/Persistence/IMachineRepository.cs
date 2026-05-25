using Aurik.Monitoring.Domain.Entities;

namespace Aurik.Monitoring.Application.Abstractions.Persistence;

public interface IMachineRepository
{
    Task<Machine?> GetAsync(string machineId, CancellationToken ct);
    Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken ct);
    Task UpsertManyAsync(IReadOnlyList<Machine> machines, CancellationToken ct);
}
