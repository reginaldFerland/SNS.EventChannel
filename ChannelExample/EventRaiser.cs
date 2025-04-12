namespace Event;

public class EventRaiser
{
    private readonly Random _random;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;

    public EventRaiser(int minDelayMs = 100, int maxDelayMs = 1000)
    {
        _random = new Random();
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public async Task SingleEvent(Guid id, CancellationToken cancellationToken = default)
    {
        // Simulate random processing time
        int delayMs = _random.Next(_minDelayMs, _maxDelayMs);
        await Task.Delay(delayMs, cancellationToken);
    }

    public async Task BulkEvent(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        // For bulk lookup, we simulate a single wait for the entire batch
        int delayMs = _random.Next(_minDelayMs, _maxDelayMs);
        await Task.Delay(delayMs, cancellationToken);

        if (ids.Count() > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(ids), "Bulk Event supports a maximum of 10 IDs.");
        }

    }
}