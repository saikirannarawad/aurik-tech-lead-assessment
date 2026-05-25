using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aurik.Monitoring.Application.BackgroundProcessing;

/// <summary>
/// In-process bounded queue using System.Threading.Channels. Bounded backpressure prevents
/// runaway memory if the worker stalls; producers will await when full.
/// </summary>
public sealed class ChannelProcessingQueue : IProcessingQueue
{
    private readonly Channel<ProcessingWorkItem> _channel;

    public ChannelProcessingQueue(int capacity = 1024)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<ProcessingWorkItem>(options);
    }

    public ValueTask EnqueueAsync(ProcessingWorkItem item, CancellationToken ct) =>
        _channel.Writer.WriteAsync(item, ct);

    public async IAsyncEnumerable<ProcessingWorkItem> DequeueAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }
}
