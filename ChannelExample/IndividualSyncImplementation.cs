using ChannelExample;
using GuidLookup;
using Event;

namespace SyncExample;

public class SyncImplementation
{
    private readonly PersonLookup _guidLookup;
    private readonly EventRaiser _eventRaiser;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;

    public SyncImplementation(int minDelayMs = 100, int maxDelayMs = 1000)
    {
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        _eventRaiser = new EventRaiser(minDelayMs, maxDelayMs);
        _guidLookup = new PersonLookup(minDelayMs, maxDelayMs);
    }

    public async Task HandleIndividually_Foreach(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        foreach (var guid in guids)
        {
            var person = await _guidLookup.SingleLookup(guid, cancellationToken);
            await _eventRaiser.SingleEvent(guid, cancellationToken);
        }
    }

    public async Task HandleIndividually_Functional(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        var personList = new List<Person>();

        var tasks = guids.Select(async guid =>
        {
            var person = await _guidLookup.SingleLookup(guid, cancellationToken);
            await _eventRaiser.SingleEvent(guid, cancellationToken);
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
                await _eventRaiser.BulkEvent(personChunk.Select(p => p.Id), cancellationToken);
            }
        }
    }

    public async Task HandleBulk_Functional(CancellationToken cancellationToken = default)
    {
        var guids = GuidGenerator.GenerateOneMillionGuids();

        var personList = new List<Person>();

        var tasks = guids.Chunk(10_000).Select(async chunk =>
        {
            var person = await _guidLookup.BulkLookup(chunk, cancellationToken);
            var personBatch = person.Chunk(10)
                .Select(async personChunk =>
                {
                    await _eventRaiser.BulkEvent(personChunk.Select(p => p.Id), cancellationToken);
                });
            await Task.WhenAll(personBatch);
            return person;
        });

        var people = await Task.WhenAll(tasks);

        foreach (var person in people)
        {
            personList.AddRange(person);
        }

    }

}