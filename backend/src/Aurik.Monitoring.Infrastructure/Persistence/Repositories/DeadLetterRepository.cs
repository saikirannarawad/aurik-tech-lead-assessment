using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class DeadLetterRepository : IDeadLetterRepository
{
    private readonly MongoContext _ctx;

    public DeadLetterRepository(MongoContext ctx) => _ctx = ctx;

    public Task InsertAsync(DeadLetterEntry entry, CancellationToken ct) =>
        _ctx.DeadLetters.InsertOneAsync(entry, cancellationToken: ct);

    public async Task<IReadOnlyList<DeadLetterEntry>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var docs = await _ctx.DeadLetters
            .Find(FilterDefinition<DeadLetterEntry>.Empty)
            .SortByDescending(d => d.DeadLetteredAt)
            .Limit(limit)
            .ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }
}
