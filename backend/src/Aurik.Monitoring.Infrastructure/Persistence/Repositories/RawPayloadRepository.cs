using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence.Repositories;

public sealed class RawPayloadRepository : IRawPayloadRepository
{
    private readonly MongoContext _ctx;

    public RawPayloadRepository(MongoContext ctx) => _ctx = ctx;

    public async Task<(RawPayload payload, bool isNew)> InsertIfNotExistsAsync(RawPayload payload, CancellationToken ct)
    {
        try
        {
            await _ctx.RawPayloads.InsertOneAsync(payload, cancellationToken: ct).ConfigureAwait(false);
            return (payload, true);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Idempotency hit — return the existing record.
            var existing = await _ctx.RawPayloads
                .Find(p => p.IdempotencyKey == payload.IdempotencyKey)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            return (existing ?? payload, false);
        }
    }

    public Task<RawPayload?> GetAsync(string id, CancellationToken ct) =>
        _ctx.RawPayloads.Find(p => p.Id == id).FirstOrDefaultAsync(ct)!;

    public async Task UpdateStateAsync(string id, ProcessingState state, string? failureReason, int attemptCount, CancellationToken ct)
    {
        var update = Builders<RawPayload>.Update
            .Set(p => p.State, state)
            .Set(p => p.FailureReason, failureReason)
            .Set(p => p.AttemptCount, attemptCount)
            .Set(p => p.LastAttemptAt, DateTime.UtcNow);
        await _ctx.RawPayloads.UpdateOneAsync(p => p.Id == id, update, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RawPayload>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var docs = await _ctx.RawPayloads
            .Find(FilterDefinition<RawPayload>.Empty)
            .SortByDescending(p => p.ReceivedAt)
            .Limit(limit)
            .ToListAsync(ct).ConfigureAwait(false);
        return docs;
    }
}
