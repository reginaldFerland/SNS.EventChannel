using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace SNS.EventChannel.Tests;

public class EventRaiserTests
{
    private class TestEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = "Test Message";
    }

    private class AnotherTestEvent
    {
        public int Value { get; set; } = 123;
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var channels = new List<IEventChannel>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new EventRaiser(channels, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithChannels_RegistersChannels()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var channel1 = new EventChannel<TestEvent>();
        var channel2 = new EventChannel<AnotherTestEvent>();
        var channels = new List<IEventChannel> { channel1, channel2 };

        // Act
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Assert
        // We'll verify this through the RaiseEvent method below
        Assert.NotNull(eventRaiser);
    }

    [Fact]
    public async Task RegisterChannel_AddsChannelToDictionary()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var channels = new List<IEventChannel>();
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);
        var newChannel = new EventChannel<TestEvent>();

        // Act
        eventRaiser.RegisterChannel(newChannel);

        // Assert
        // Verify by using the channel we just registered
        var result = await eventRaiser.RaiseEvent(new TestEvent());
        Assert.True(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Registered event channel for type TestEvent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RaiseEvent_WithRegisteredChannel_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var testEvent = new TestEvent();
        var channel = new EventChannel<TestEvent>();
        var channels = new List<IEventChannel> { channel };
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Act
        var result = await eventRaiser.RaiseEvent(testEvent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RaiseEvent_WithUnregisteredChannel_ReturnsFalse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var testEvent = new TestEvent();
        var channels = new List<IEventChannel>(); // Empty channels collection
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Act
        var result = await eventRaiser.RaiseEvent(testEvent);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No event channel configured for type TestEvent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RaiseEvent_WithNullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var channels = new List<IEventChannel>();
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => eventRaiser.RaiseEvent<TestEvent>(null!));
        Assert.Equal("event", exception.ParamName);
    }

    [Fact]
    public async Task RaiseEvents_WithRegisteredChannel_ProcessesAllEvents()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var events = new List<TestEvent>
        {
            new TestEvent { Message = "Event 1" },
            new TestEvent { Message = "Event 2" },
            new TestEvent { Message = "Event 3" }
        };
        var channel = new EventChannel<TestEvent>();
        var channels = new List<IEventChannel> { channel };
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Act
        await eventRaiser.RaiseEvents(events);

        // Assert - verify all events were added to the channel
        var reader = channel.GetReader();
        var count = 0;
        while (await reader.WaitToReadAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token))
        {
            if (reader.TryRead(out var _))
            {
                count++;
            }

            if (count == events.Count)
                break;
        }
        Assert.Equal(events.Count, count);
    }

    [Fact]
    public async Task RaiseEvents_WithUnregisteredChannel_LogsWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();
        var events = new List<TestEvent> { new TestEvent(), new TestEvent() };
        var channels = new List<IEventChannel>(); // Empty channels collection
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);

        // Act
        await eventRaiser.RaiseEvents(events);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No event channel configured for type TestEvent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RaiseEvent_WithWrongChannelType_LogsError()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();

        // Create a mock IEventChannel that returns TestEvent type but is not actually EventChannel<TestEvent>
        var mockChannel = new Mock<IEventChannel>();
        mockChannel.Setup(c => c.EventType).Returns(typeof(TestEvent));

        var channels = new List<IEventChannel> { mockChannel.Object };
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);
        var testEvent = new TestEvent();

        // Act
        var result = await eventRaiser.RaiseEvent(testEvent);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Channel found for type TestEvent but could not cast to EventChannel<TestEvent>")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RaiseEvents_WithWrongChannelType_LogsError()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EventRaiser>>();

        // Create a mock IEventChannel that returns TestEvent type but is not actually EventChannel<TestEvent>
        var mockChannel = new Mock<IEventChannel>();
        mockChannel.Setup(c => c.EventType).Returns(typeof(TestEvent));

        var channels = new List<IEventChannel> { mockChannel.Object };
        var eventRaiser = new EventRaiser(channels, mockLogger.Object);
        var events = new List<TestEvent> { new TestEvent(), new TestEvent() };

        // Act
        await eventRaiser.RaiseEvents(events);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Channel found for type TestEvent but could not cast to EventChannel<TestEvent>")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}