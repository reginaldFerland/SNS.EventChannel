using System.Threading.Channels;

namespace SNS.EventChannel;

public interface IEventChannel
{
    Type EventType { get; }
}
/// <summary>
/// A generic event channel that processes items of type T using System.Threading.Channels
/// </summary>
/// <typeparam name="T">The type of items that will be processed by this channel</typeparam>
public class EventChannel<T> : IEventChannel
{
    public Type EventType => typeof(T);
    private readonly Channel<T> _channel;

    /// <summary>
    /// Creates a new instance of the EventChannel
    /// </summary>
    /// <param name="boundedCapacity">The maximum number of items that can be stored in the channel</param>
    public EventChannel(int boundedCapacity = 1_000_000)
    {
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(boundedCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Writes an item to the channel
    /// </summary>
    /// <param name="item">The item to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the item was written, false otherwise</returns>
    public async Task<bool> WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        if (await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            return _channel.Writer.TryWrite(item);
        }
        return false;
    }

    /// <summary>
    /// Writes multiple items to the channel
    /// </summary>
    /// <param name="items">The collection of items to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteAllAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await WriteAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the reader for this channel
    /// </summary>
    /// <returns>The channel reader</returns>
    public ChannelReader<T> GetReader()
    {
        return _channel.Reader;
    }
}

