namespace GuidLookup;
public class Person
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public DateTime Created { get; set; }
}

public class PersonLookup
{
    private readonly Random _random;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;

    public PersonLookup(int minDelayMs = 100, int maxDelayMs = 1000)
    {
        _random = new Random();
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public async Task<Person> SingleLookup(Guid id, CancellationToken cancellationToken = default)
    {
        // Simulate random processing time
        int delayMs = _random.Next(_minDelayMs, _maxDelayMs);
        await Task.Delay(delayMs, cancellationToken);

        // Return mock person data
        return new Person
        {
            Id = id,
            Name = $"Person {id.ToString().Substring(0, 8)}",
            Description = $"Description for {id}",
            Created = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<Person>> BulkLookup(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        // For bulk lookup, we simulate a single wait for the entire batch
        int delayMs = _random.Next(_minDelayMs, _maxDelayMs);
        await Task.Delay(delayMs, cancellationToken);

        if (ids.Count() > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(ids), "Bulk lookup supports a maximum of 10,000 IDs.");
        }

        var results = new List<Person>();
        foreach (var id in ids)
        {
            results.Add(new Person
            {
                Id = id,
                Name = $"Person {id.ToString().Substring(0, 8)}",
                Description = $"Description for {id}",
                Created = DateTime.UtcNow
            });
        }

        return results;
    }
}