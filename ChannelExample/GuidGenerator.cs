namespace ChannelExample;
/// <summary>
/// A class that generates a list of random GUIDs
/// </summary>
public class GuidGenerator
{
    /// <summary>
    /// Generates a list containing the specified number of random GUIDs
    /// </summary>
    /// <param name="count">The number of GUIDs to generate</param>
    /// <returns>A list of random GUIDs</returns>
    public static List<Guid> GenerateRandomGuids(int count)
    {
        List<Guid> guids = new(count);

        for (int i = 0; i < count; i++)
        {
            guids.Add(Guid.NewGuid());
        }

        return guids;
    }

    /// <summary>
    /// Generates a list of 1 million random GUIDs
    /// </summary>
    /// <returns>A list containing 1 million random GUIDs</returns>
    public static List<Guid> GenerateOneMillionGuids()
    {
        return GenerateRandomGuids(100);
    }
}