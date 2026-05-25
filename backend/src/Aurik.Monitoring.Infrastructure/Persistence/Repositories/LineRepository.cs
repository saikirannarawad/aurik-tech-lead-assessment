using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class LineRepository : ILineRepository
{
    private readonly MongoContext _ctx;

    public LineRepository(MongoContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<Line>> GetAllAsync(CancellationToken ct)
    {
        var docs = await _ctx.Lines.Find(FilterDefinition<Line>.Empty).ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }

    public async Task UpsertManyAsync(IReadOnlyList<Line> lines, CancellationToken ct)
    {
        if (lines.Count == 0) return;
        var models = lines.Select(l =>
            new ReplaceOneModel<Line>(
                Builders<Line>.Filter.And(
                    Builders<Line>.Filter.Eq(x => x.PlantId, l.PlantId),
                    Builders<Line>.Filter.Eq(x => x.LineId, l.LineId)),
                l) { IsUpsert = true }).ToList();
        await _ctx.Lines.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false }, ct).ConfigureAwait(false);
    }
}
