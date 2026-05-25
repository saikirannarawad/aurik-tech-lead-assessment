namespace Aurik.Monitoring.Application.BackgroundProcessing;

/// <summary>
/// Hands off accepted payloads to the background worker. Backed by System.Threading.Channels.
/// </summary>
public interface IProcessingQueue
{
    ValueTask EnqueueAsync(ProcessingWorkItem item, CancellationToken ct);

    IAsyncEnumerable<ProcessingWorkItem> DequeueAllAsync(CancellationToken ct);
}

public sealed record ProcessingWorkItem(string RawPayloadId, int AttemptNumber);
