using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SNS.EventChannel.Tests;
public class EventChannelTests
{
    [Fact]
    public void Constructor_InitializesEventType_ReturnsCorrectType()
    {
        // Arrange & Act
        var eventChannel = new EventChannel<string>();

        // Assert
        Assert.Equal(typeof(string), eventChannel.EventType);
    }

    [Fact]
    public void Constructor_WithBoundedCapacity_CreatesEventChannel()
    {
        // Arrange & Act
        var eventChannel = new EventChannel<int>(100);

        // Assert
        Assert.NotNull(eventChannel);
        Assert.Equal(typeof(int), eventChannel.EventType);
    }

    [Fact]
    public async Task WriteAsync_WithValidItem_ReturnsTrue()
    {
        // Arrange
        var eventChannel = new EventChannel<string>();
        var item = "test message";

        // Act
        var result = await eventChannel.WriteAsync(item);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task WriteAllAsync_WritesMultipleItemsToChannel()
    {
        // Arrange
        var eventChannel = new EventChannel<int>();
        var items = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        await eventChannel.WriteAllAsync(items);
        var reader = eventChannel.GetReader();

        // Assert
        var count = 0;
        while (await reader.WaitToReadAsync())
        {
            if (reader.TryRead(out var item))
            {
                Assert.Contains(item, items);
                count++;
            }

            if (count == items.Count)
                break;
        }

        Assert.Equal(items.Count, count);
    }

    [Fact]
    public async Task GetReader_ReturnsChannelReader_ThatReadsWrittenItems()
    {
        // Arrange
        var eventChannel = new EventChannel<string>();
        var testMessage = "test message";

        // Act
        await eventChannel.WriteAsync(testMessage);
        var reader = eventChannel.GetReader();

        // Assert
        Assert.True(await reader.WaitToReadAsync());
        Assert.True(reader.TryRead(out var result));
        Assert.Equal(testMessage, result);
    }

}
