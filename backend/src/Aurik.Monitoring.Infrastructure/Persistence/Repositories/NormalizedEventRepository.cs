using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class NormalizedEventRepository : INormalizedEventRepository
{
    private readonly MongoContext _ctx;

    public NormalizedEventRepository(MongoContext ctx) => _ctx = ctx;

    public async Task<int> InsertManyIdempotentAsync(IReadOnlyList<NormalizedEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return 0;

        // Unordered bulk insert lets the server skip duplicates and continue inserting the rest.
        var models = events.Select(e => new InsertOneModel<NormalizedEvent>(e)).ToList();
        try
        {
            var result = await _ctx.NormalizedEvents.BulkWriteAsync(
                models,
                new BulkWriteOptions { IsOrdered = false },
                ct).ConfigureAwait(false);
            return (int)result.InsertedCount;
        }
        catch (MongoBulkWriteException<NormalizedEvent> ex)
        {
            // Duplicates surface as write errors; that's expected idempotency behavior, not a failure.
            return (int)ex.Result.InsertedCount;
        }
    }

    public async Task<IReadOnlyList<NormalizedEvent>> GetLatestForMachineAsync(string machineId, int limit, CancellationToken ct)
    {
        var docs = await _ctx.NormalizedEvents
            .Find(e => e.MachineId == machineId)
            .SortByDescending(e => e.EventTimeUtc)
            .Limit(limit)
            .ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }

    public async Task<IReadOnlyList<NormalizedEvent>> GetSinceAsync(string machineId, DateTime since, CancellationToken ct)
    {
        var docs = await _ctx.NormalizedEvents
            .Find(e => e.MachineId == machineId && e.EventTimeUtc >= since)
            .SortByDescending(e => e.EventTimeUtc)
            .ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }
}
