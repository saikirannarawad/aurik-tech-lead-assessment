using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Application.DerivedView;
using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aurik.Monitoring.Application.Processing;

public sealed class PayloadProcessor : IPayloadProcessor
{
    private readonly IRawPayloadRepository _rawRepo;
    private readonly INormalizedEventRepository _eventRepo;
    private readonly IMachineRepository _machineRepo;
    private readonly IMachineViewRepository _viewRepo;
    private readonly INormalizerFactory _factory;
    private readonly IDerivedStatusCalculator _calculator;
    private readonly ILogger<PayloadProcessor> _log;

    /// <summary>Optimistic concurrency: re-read & retry up to N times before failing.</summary>
    private const int MaxConcurrencyRetries = 3;

    public PayloadProcessor(
        IRawPayloadRepository rawRepo,
        INormalizedEventRepository eventRepo,
        IMachineRepository machineRepo,
        IMachineViewRepository viewRepo,
        INormalizerFactory factory,
        IDerivedStatusCalculator calculator,
        ILogger<PayloadProcessor> log)
    {
        _rawRepo = rawRepo;
        _eventRepo = eventRepo;
        _machineRepo = machineRepo;
        _viewRepo = viewRepo;
        _factory = factory;
        _calculator = calculator;
        _log = log;
    }

    public async Task<ProcessingOutcome> ProcessAsync(string rawPayloadId, int attemptNumber, CancellationToken ct)
    {
        var raw = await _rawRepo.GetAsync(rawPayloadId, ct).ConfigureAwait(false);
        if (raw is null)
            return new ProcessingOutcome(false, 0, 0, $"Raw payload {rawPayloadId} not found", Retryable: false);

        await _rawRepo.UpdateStateAsync(rawPayloadId, ProcessingState.Processing, null, attemptNumber, ct)
            .ConfigureAwait(false);

        NormalizationResult result;
        try
        {
            var normalizer = _factory.GetFor(raw.Vendor);
            result = normalizer.Normalize(raw.RawJson, raw.Id);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
        {
            _log.LogWarning(ex, "Malformed payload {Id} ({Vendor})", raw.Id, raw.Vendor);
            return new ProcessingOutcome(false, 0, 0, $"Malformed payload: {ex.Message}", Retryable: false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Normalization failed for payload {Id}", raw.Id);
            return new ProcessingOutcome(false, 0, 0, $"Normalization error: {ex.Message}", Retryable: true);
        }

        var insertedCount = 0;
        if (result.Events.Count > 0)
        {
            insertedCount = await _eventRepo.InsertManyIdempotentAsync(result.Events, ct).ConfigureAwait(false);
        }

        var affectedMachines = result.Events.Select(e => e.MachineId).Distinct().ToList();
        foreach (var machineId in affectedMachines)
        {
            await RecomputeMachineViewAsync(machineId, ct).ConfigureAwait(false);
        }

        var finalState = result.Issues.Count == 0
            ? ProcessingState.Succeeded
            : (result.Events.Count == 0 ? ProcessingState.Failed : ProcessingState.PartiallySucceeded);

        var reason = result.Issues.Count == 0
            ? null
            : $"{result.Issues.Count} issues: {string.Join("; ", result.Issues.Take(5).Select(i => $"{i.Locator}={i.Reason}"))}";

        await _rawRepo.UpdateStateAsync(rawPayloadId, finalState, reason, attemptNumber, ct).ConfigureAwait(false);

        return new ProcessingOutcome(
            Success: finalState != ProcessingState.Failed,
            NormalizedCount: insertedCount,
            IssueCount: result.Issues.Count,
            FailureReason: reason,
            Retryable: false);
    }

    private async Task RecomputeMachineViewAsync(string machineId, CancellationToken ct)
    {
        var machine = await _machineRepo.GetAsync(machineId, ct).ConfigureAwait(false);
        if (machine is null)
        {
            _log.LogWarning("Skipping view recompute: machine {MachineId} not in reference data", machineId);
            return;
        }

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var existing = await _viewRepo.GetAsync(machineId, ct).ConfigureAwait(false);
            var expectedVersion = existing?.Version ?? 0;

            // Pull events from a wide enough window to cover staleness + attention rules.
            var since = DateTime.UtcNow - DerivedStatusCalculator.StaleAfter;
            var events = await _eventRepo.GetSinceAsync(machineId, since, ct).ConfigureAwait(false);

            var computed = _calculator.Compute(machine, events, DateTime.UtcNow);
            computed.Version = expectedVersion + 1;

            var ok = await _viewRepo.UpsertWithVersionAsync(computed, expectedVersion, ct).ConfigureAwait(false);
            if (ok) return;

            _log.LogInformation("Optimistic concurrency conflict on machine {MachineId}, retrying", machineId);
        }

        _log.LogWarning("Gave up recomputing view for machine {MachineId} after {Retries} attempts",
            machineId, MaxConcurrencyRetries);
    }
}
