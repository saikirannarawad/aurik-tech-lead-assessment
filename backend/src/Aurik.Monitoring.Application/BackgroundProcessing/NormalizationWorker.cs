using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Application.Processing;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aurik.Monitoring.Application.BackgroundProcessing;

/// <summary>
/// Consumes the in-process processing queue. Retries with exponential backoff up to MaxAttempts,
/// then dead-letters the payload for inspection.
/// </summary>
public sealed class NormalizationWorker : BackgroundService
{
    public const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryBackoff =
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(30)
    };

    private readonly IProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NormalizationWorker> _log;

    public NormalizationWorker(
        IProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NormalizationWorker> log)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Normalization worker started.");
        await foreach (var item in _queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            // Each work item gets its own DI scope so scoped repositories/db handles are isolated.
            _ = HandleItemAsync(item, stoppingToken);
        }
    }

    private async Task HandleItemAsync(ProcessingWorkItem item, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPayloadProcessor>();
            var rawRepo = scope.ServiceProvider.GetRequiredService<IRawPayloadRepository>();
            var deadLetter = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

            var outcome = await processor.ProcessAsync(item.RawPayloadId, item.AttemptNumber, ct).ConfigureAwait(false);

            if (outcome.Success)
            {
                _log.LogInformation(
                    "Processed payload {Id} attempt {Attempt}: {Inserted} new events, {Issues} issues",
                    item.RawPayloadId, item.AttemptNumber, outcome.NormalizedCount, outcome.IssueCount);
                return;
            }

            if (outcome.Retryable && item.AttemptNumber < MaxAttempts)
            {
                var delay = RetryBackoff[Math.Min(item.AttemptNumber - 1, RetryBackoff.Length - 1)];
                _log.LogWarning(
                    "Transient failure on {Id} attempt {Attempt}: {Reason}; retrying in {Delay}",
                    item.RawPayloadId, item.AttemptNumber, outcome.FailureReason, delay);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                await _queue.EnqueueAsync(item with { AttemptNumber = item.AttemptNumber + 1 }, ct)
                    .ConfigureAwait(false);
                return;
            }

            await rawRepo.UpdateStateAsync(item.RawPayloadId, ProcessingState.DeadLettered, outcome.FailureReason,
                item.AttemptNumber, ct).ConfigureAwait(false);

            await deadLetter.InsertAsync(new DeadLetterEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                RawPayloadId = item.RawPayloadId,
                Vendor = (await rawRepo.GetAsync(item.RawPayloadId, ct))?.Vendor ?? VendorType.Unknown,
                DeadLetteredAt = DateTime.UtcNow,
                Reason = outcome.FailureReason ?? "Unknown",
                AttemptCount = item.AttemptNumber,
                StackTrace = null
            }, ct).ConfigureAwait(false);

            _log.LogError("Dead-lettered payload {Id} after {Attempt} attempts: {Reason}",
                item.RawPayloadId, item.AttemptNumber, outcome.FailureReason);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled error while processing {Id}", item.RawPayloadId);
        }
    }
}
