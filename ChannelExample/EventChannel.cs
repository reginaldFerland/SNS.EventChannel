using System.Threading.Channels;
using Event;

namespace ChannelExample;

public class GuidChannel
{
    private readonly Channel<Guid> _channel;
    private readonly int _boundedCapacity;
    private readonly EventRaiser _eventRaiser;
    private Task? _processingTask;
    private CancellationTokenSource _cts;

    public GuidChannel(int boundedCapacity = 1_000_000, int minDelayMs = 100, int maxDelayMs = 1000)
    {
        _boundedCapacity = boundedCapacity;
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(boundedCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        _eventRaiser = new EventRaiser(minDelayMs, maxDelayMs);
        _cts = new CancellationTokenSource();

        // Start the processing task when the channel is created
        _processingTask = ProcessChannelAsync(_cts.Token);
    }

    public async Task<bool> WriteAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        if (await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            return _channel.Writer.TryWrite(guid);
        }
        return false;
    }

    public async Task WriteAllAsync(IEnumerable<Guid> guids, CancellationToken cancellationToken = default)
    {
        foreach (var guid in guids)
        {
            await WriteAsync(guid, cancellationToken);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _channel.Writer.Complete();
        await _channel.Reader.Completion;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        _channel.Writer.Complete();

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _cts.Dispose();
    }

    private async Task ProcessChannelAsync(CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;
        var tasks = new List<Task>();

        // Buffer to accumulate items for batch processing
        List<Guid> batchBuffer = new List<Guid>(10);

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            // Read up to 10 items (the max batch size for BulkEvent)
            while (reader.TryRead(out var guid))
            {
                batchBuffer.Add(guid);

                // When we have 10 items or if no more items are available, process the batch
                if (batchBuffer.Count == 10 || !reader.TryPeek(out _))
                {
                    if (batchBuffer.Count > 0)
                    {
                        // Process the batch
                        await _eventRaiser.BulkEvent(batchBuffer, cancellationToken);
                        batchBuffer.Clear();
                    }
                }
            }

            // Process any remaining items in the buffer
            if (batchBuffer.Count > 0)
            {
                await _eventRaiser.BulkEvent(batchBuffer, cancellationToken);
                batchBuffer.Clear();
            }
        }
    }
}