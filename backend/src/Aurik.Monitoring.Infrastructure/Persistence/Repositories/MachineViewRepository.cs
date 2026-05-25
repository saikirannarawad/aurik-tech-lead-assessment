using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class MachineViewRepository : IMachineViewRepository
{
    private readonly MongoContext _ctx;

    public MachineViewRepository(MongoContext ctx) => _ctx = ctx;

    public Task<MachineOperationalView?> GetAsync(string machineId, CancellationToken ct) =>
        _ctx.MachineViews.Find(v => v.MachineId == machineId).FirstOrDefaultAsync(ct)!;

    /// <summary>
    /// Optimistic concurrency:
    ///   - If doc absent (expectedVersion == 0): insert.
    ///   - If doc present at expectedVersion: replace.
    ///   - Otherwise: write nothing and return false so caller can re-read & retry.
    /// </summary>
    public async Task<bool> UpsertWithVersionAsync(MachineOperationalView view, long expectedVersion, CancellationToken ct)
    {
        var filter = Builders<MachineOperationalView>.Filter.And(
            Builders<MachineOperationalView>.Filter.Eq(v => v.MachineId, view.MachineId),
            Builders<MachineOperationalView>.Filter.Eq(v => v.Version, expectedVersion));

        var result = await _ctx.MachineViews.ReplaceOneAsync(
            filter,
            view,
            new ReplaceOptions { IsUpsert = false },
            ct).ConfigureAwait(false);

        if (result.MatchedCount == 1) return true;

        if (expectedVersion == 0)
        {
            // First-write path — try to insert; fall back to false if someone beat us to it.
            try
            {
                await _ctx.MachineViews.InsertOneAsync(view, cancellationToken: ct).ConfigureAwait(false);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<MachineOperationalView>> QueryAsync(
        string? plantId,
        string? lineId,
        DerivedStatus? status,
        AttentionLevel? minAttention,
        int limit,
        CancellationToken ct)
    {
        var filters = new List<FilterDefinition<MachineOperationalView>>();
        if (!string.IsNullOrWhiteSpace(plantId))
            filters.Add(Builders<MachineOperationalView>.Filter.Eq(v => v.PlantId, plantId));
        if (!string.IsNullOrWhiteSpace(lineId))
            filters.Add(Builders<MachineOperationalView>.Filter.Eq(v => v.LineId, lineId));
        if (status.HasValue)
            filters.Add(Builders<MachineOperationalView>.Filter.Eq(v => v.DerivedStatus, status.Value));
        if (minAttention.HasValue)
            filters.Add(Builders<MachineOperationalView>.Filter.Gte(v => v.AttentionLevel, minAttention.Value));

        var filter = filters.Count == 0
            ? FilterDefinition<MachineOperationalView>.Empty
            : Builders<MachineOperationalView>.Filter.And(filters);

        var docs = await _ctx.MachineViews
            .Find(filter)
            .SortByDescending(v => v.AttentionLevel)
            .Limit(limit)
            .ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }
}
