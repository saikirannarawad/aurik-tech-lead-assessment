using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Application.BackgroundProcessing;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aurik.Monitoring.Application.Ingestion;

public sealed class IngestionService : IIngestionService
{
    private readonly IRawPayloadRepository _rawRepo;
    private readonly IProcessingQueue _queue;
    private readonly ILogger<IngestionService> _log;

    public IngestionService(
        IRawPayloadRepository rawRepo,
        IProcessingQueue queue,
        ILogger<IngestionService> log)
    {
        _rawRepo = rawRepo;
        _queue = queue;
        _log = log;
    }

    public async Task<IngestionAck> AcceptAsync(
        VendorType vendor,
        string rawJson,
        string idempotencyKey,
        string sourceIp,
        CancellationToken ct)
    {
        var payload = new RawPayload
        {
            Id = Guid.NewGuid().ToString("N"),
            Vendor = vendor,
            IdempotencyKey = idempotencyKey,
            RawJson = rawJson,
            ReceivedAt = DateTime.UtcNow,
            SourceIp = sourceIp,
            RecordCount = EstimateRecordCount(rawJson, vendor),
            State = ProcessingState.Queued
        };

        var (stored, isNew) = await _rawRepo.InsertIfNotExistsAsync(payload, ct).ConfigureAwait(false);

        if (!isNew)
        {
            _log.LogInformation("Duplicate payload for idempotency key {Key}; returning existing id {Id}",
                idempotencyKey, stored.Id);
            return new IngestionAck(stored.Id, ProcessingState.Duplicate, Duplicate: true, stored.RecordCount);
        }

        await _queue.EnqueueAsync(new ProcessingWorkItem(stored.Id, AttemptNumber: 1), ct).ConfigureAwait(false);

        return new IngestionAck(stored.Id, ProcessingState.Queued, Duplicate: false, stored.RecordCount);
    }

    private static int EstimateRecordCount(string rawJson, VendorType vendor)
    {
        // Cheap O(n) count of the array name occurrence is enough for an at-receipt estimate.
        // Final counts come from the normalizer.
        var marker = vendor switch
        {
            VendorType.PulseForge => "\"events\"",
            VendorType.ThermexWatch => "\"readings\"",
            VendorType.MaintaFlow => "\"records\"",
            _ => null
        };
        if (marker is null) return 0;
        var idx = rawJson.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 0;
        var slice = rawJson.AsSpan(idx);
        var count = 0;
        var inObject = 0;
        var seenArrayStart = false;
        foreach (var ch in slice)
        {
            if (!seenArrayStart)
            {
                if (ch == '[') seenArrayStart = true;
                continue;
            }
            if (ch == '{')
            {
                if (inObject == 0) count++;
                inObject++;
            }
            else if (ch == '}')
            {
                inObject--;
            }
            else if (ch == ']' && inObject == 0)
            {
                break;
            }
        }
        return count;
    }
}
