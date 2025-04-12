using ChannelExample;
using GuidLookup;

namespace AsyncExample;

public class AsyncImplementation
{
    private readonly PersonLookup _guidLookup;
    private readonly GuidChannel _guidChannel;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;

    public AsyncImplementation(GuidChannel channel, int minDelayMs = 100, int maxDelayMs = 1000)
    {
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        _guidLookup = new PersonLookup(minDelayMs, maxDelayMs);
        _guidChannel = channel;
    }

    public async Task HandleIndividually_Foreach(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        foreach (var guid in guids)
        {
            var person = await _guidLookup.SingleLookup(guid, cancellationToken);
            await _guidChannel.WriteAsync(guid, cancellationToken);
        }
    }

    public async Task HandleIndividually_Functional(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        var personList = new List<Person>();

        var tasks = guids.Select(async guid =>
        {
            var person = await _guidLookup.SingleLookup(guid, cancellationToken);
            await _guidChannel.WriteAsync(guid, cancellationToken);
            personList.Add(person);
        });

        await Task.WhenAll(tasks);

    }

    public async Task HandleBulk_Foreach(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        var batch = guids.Chunk(10_000);

        foreach (var chunk in batch)
        {
            var person = await _guidLookup.BulkLookup(chunk, cancellationToken);
            var personBatch = person.Chunk(10);
            foreach (var personChunk in personBatch)
            {
                await _guidChannel.WriteAllAsync(personChunk.Select(p => p.Id), cancellationToken);
            }
        }
    }

    public async Task HandleBulk_Functional(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        var personList = new List<Person>();

        var tasks = guids.Chunk(10_000).Select(async chunk =>
        {
            var persons = await _guidLookup.BulkLookup(chunk, cancellationToken);

            var personChunkTasks = persons.Chunk(10)
                .Select(personChunk => _guidChannel.WriteAllAsync(personChunk.Select(p => p.Id), cancellationToken));

            await Task.WhenAll(personChunkTasks);

            // Return the persons for later collection
            return persons;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Collect all persons after all tasks are complete
        foreach (var persons in results)
        {
            personList.AddRange(persons);
        }
    }

}