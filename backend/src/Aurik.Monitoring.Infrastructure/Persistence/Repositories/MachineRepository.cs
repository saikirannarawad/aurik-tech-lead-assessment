using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class MachineRepository : IMachineRepository
{
    private readonly MongoContext _ctx;

    public MachineRepository(MongoContext ctx) => _ctx = ctx;

    public Task<Machine?> GetAsync(string machineId, CancellationToken ct) =>
        _ctx.Machines.Find(m => m.MachineId == machineId).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken ct)
    {
        var docs = await _ctx.Machines.Find(FilterDefinition<Machine>.Empty).ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }

    public async Task UpsertManyAsync(IReadOnlyList<Machine> machines, CancellationToken ct)
    {
        if (machines.Count == 0) return;
        var models = machines.Select(m =>
            new ReplaceOneModel<Machine>(
                Builders<Machine>.Filter.Eq(x => x.MachineId, m.MachineId),
                m) { IsUpsert = true }).ToList();
        await _ctx.Machines.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false }, ct).ConfigureAwait(false);
    }
}
