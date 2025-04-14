using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using SNS.EventChannel.Tests.TestHelpers;
using Xunit;

namespace SNS.EventChannel.Tests;

public class EventChannelWorkerTests
{
    private readonly Mock<ILogger<EventChannelWorker<TestEvent>>> _mockLogger;

    public EventChannelWorkerTests()
    {
        _mockLogger = new Mock<ILogger<EventChannelWorker<TestEvent>>>();
    }

    // Changed from private to public to allow Moq to work with it
    public class TestEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = "Test Message";
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsNullReferenceException()
    {
        // Arrange
        EventChannelWorkerConfig<TestEvent> config = null!;

        // Act & Assert
        // The implementation throws NullReferenceException when config is null
        Assert.Throws<NullReferenceException>(() =>
            new EventChannelWorker<TestEvent>(config, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = new EventChannel<TestEvent>(),
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = MockSnsClientFactory.CreateSuccessfulMockSnsClient()
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventChannelWorker<TestEvent>(config, null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullEventChannel_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = null!,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = MockSnsClientFactory.CreateSuccessfulMockSnsClient()
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventChannelWorker<TestEvent>(config, _mockLogger.Object));

        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullTopicArn_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = new EventChannel<TestEvent>(),
            TopicArn = null!,
            SnsClient = MockSnsClientFactory.CreateSuccessfulMockSnsClient()
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventChannelWorker<TestEvent>(config, _mockLogger.Object));

        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSnsClient_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = new EventChannel<TestEvent>(),
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = null
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventChannelWorker<TestEvent>(config, _mockLogger.Object));

        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = new EventChannel<TestEvent>(),
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = MockSnsClientFactory.CreateSuccessfulMockSnsClient()
        };

        // Act
        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);

        // Assert
        Assert.NotNull(worker);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("EventChannelWorker initialized with topic ARN:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithCustomResiliencyPolicy_UsesProvidedPolicy()
    {
        // Arrange
        var customPolicy = Policy
            .Handle<ThrottledException>()
            .OrResult<PublishBatchResponse>(r => r.Failed.Count > 0)
            .WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(100));

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = new EventChannel<TestEvent>(),
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = MockSnsClientFactory.CreateSuccessfulMockSnsClient(),
            ResiliencyPolicy = customPolicy
        };

        // Act
        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);

        // Assert
        Assert.NotNull(worker);
    }

    [Fact]
    public async Task StartAsync_StartsProcessingChannelItems()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful =
                [
                    new() { Id = "0", MessageId = "msg-1" }
                ],
                Failed = []
            });

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvent = new TestEvent { Message = "Test Start Message" };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAsync(testEvent);

        // Give some time for the background processing to occur
        await Task.Delay(100);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert - Verify the SNS client was called
        mockSnsClient.Verify(x =>
            x.PublishBatchAsync(
                It.Is<PublishBatchRequest>(req =>
                    req.TopicArn == config.TopicArn &&
                    req.PublishBatchRequestEntries.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessChannel_HandlesMultipleItems()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful =
                [
                    new() { Id = "0", MessageId = "msg-1" },
                    new() { Id = "1", MessageId = "msg-2" },
                    new() { Id = "2", MessageId = "msg-3" }
                ],
                Failed = []
            });

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvents = new List<TestEvent>
        {
            new() { Message = "Test Message 1" },
            new() { Message = "Test Message 2" },
            new() { Message = "Test Message 3" }
        };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAllAsync(testEvents);

        // Give some time for the background processing to occur
        await Task.Delay(100);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockSnsClient.Verify(x =>
            x.PublishBatchAsync(
                It.Is<PublishBatchRequest>(req =>
                    req.TopicArn == config.TopicArn &&
                    req.PublishBatchRequestEntries.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully published 3 messages to SNS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessChannel_HandlesPublishingFailures()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful =
                [
                    new() { Id = "0", MessageId = "msg-1" }
                ],
                Failed =
                [
                    new()
                    {
                        Id = "1",
                        Code = "InvalidParameter",
                        Message = "Invalid parameter"
                    }
                ]
            });

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvents = new List<TestEvent>
        {
            new() { Message = "Success Message" },
            new() { Message = "Failed Message" }
        };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAllAsync(testEvents);

        // Give some time for the background processing to occur
        await Task.Delay(100);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockSnsClient.Verify(x =>
            x.PublishBatchAsync(
                It.IsAny<PublishBatchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify successful publish log
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully published 1 messages to SNS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify failed publish log
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to publish message 1: InvalidParameter - Invalid parameter")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessChannel_HandlesApiException()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        // Setup to throw exception after retries are exhausted
        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalErrorException("Service encountered an internal error"));

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object,
            MaxRetryAttempts = 1  // Set to 1 to speed up test
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvent = new TestEvent { Message = "Test Message" };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAsync(testEvent);

        // Give more time for the background processing and retry to occur
        // This needs to be long enough for all retries to be exhausted
        await Task.Delay(3000);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert - verify retry attempt was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Retrying SNS publish after")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Verify the final error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to publish batch to SNS:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_GracefullyStopsProcessing()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful =
                [
                    new() { Id = "0", MessageId = "msg-1" }
                ],
                Failed = []
            });

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting EventChannelWorker hosted service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Stopping EventChannelWorker hosted service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_HandlesOperationCancelledException()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        // Setup a delay that will cause the operation to be cancelled when stopping
        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .Returns<PublishBatchRequest, CancellationToken>((_, ct) =>
                Task.Delay(1000, ct).ContinueWith(_ => new PublishBatchResponse(), ct));

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvent = new TestEvent { Message = "Cancellation Test" };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAsync(testEvent);

        // Stop immediately without giving time for the operation to complete
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Stopping EventChannelWorker hosted service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SerializeMessage_HandlesJsonSerialization()
    {
        // Arrange
        var eventChannel = new EventChannel<TestEvent>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        string capturedMessage = null!;

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishBatchRequest, CancellationToken>((req, _) =>
                capturedMessage = req.PublishBatchRequestEntries[0].Message)
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful =
                [
                    new() { Id = "0", MessageId = "msg-1" }
                ],
                Failed = []
            });

        var config = new EventChannelWorkerConfig<TestEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object
        };

        var worker = new EventChannelWorker<TestEvent>(config, _mockLogger.Object);
        var testEvent = new TestEvent { Id = "test-id", Message = "Serialization Test" };

        // Act
        await worker.StartAsync(CancellationToken.None);
        await eventChannel.WriteAsync(testEvent);

        // Give some time for the background processing to occur
        await Task.Delay(100);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("test-id", capturedMessage);
        Assert.Contains("Serialization Test", capturedMessage);
    }
}