using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace SNS.EventChannel.Tests;
public class EventChannelIntegrationTests
{
    public class OrderCreatedEvent
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal Amount { get; set; } = 99.99m;
        public string CustomerId { get; set; } = "CUST-123";
    }

    [Fact]
    public async Task RegisterEventChannel_RaiseEvent_PublishesToSns_EndToEnd()
    {
        // Arrange
        // 1. Set up mock loggers
        var eventRaiserLogger = new Mock<ILogger<EventRaiser>>();
        var eventWorkerLogger = new Mock<ILogger<EventChannelWorker<OrderCreatedEvent>>>();

        // 2. Create a mock SNS client that captures published messages
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        var capturedMessages = new List<string>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishBatchRequest, CancellationToken>((req, _) =>
            {
                // Capture the messages for later verification
                foreach (var entry in req.PublishBatchRequestEntries)
                {
                    capturedMessages.Add(entry.Message);
                }
            })
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful = new List<PublishBatchResultEntry>
                {
                        new PublishBatchResultEntry { Id = "0", MessageId = "msg-1" }
                },
                Failed = new List<BatchResultErrorEntry>()
            });

        // 3. Set up the event channel and worker
        var eventChannel = new EventChannel<OrderCreatedEvent>();
        var workerConfig = new EventChannelWorkerConfig<OrderCreatedEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:order-events-topic",
            SnsClient = mockSnsClient.Object,
            MaxRetryAttempts = 3
        };

        var worker = new EventChannelWorker<OrderCreatedEvent>(workerConfig, eventWorkerLogger.Object);

        // 4. Create the event raiser and register the channel
        var eventRaiser = new EventRaiser(new List<IEventChannel>(), eventRaiserLogger.Object);
        eventRaiser.RegisterChannel(eventChannel);

        // Start the worker to begin processing events
        await worker.StartAsync(CancellationToken.None);

        // Act
        // 5. Create and raise an event
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = "ORD-12345",
            CreatedAt = DateTime.UtcNow,
            Amount = 199.99m,
            CustomerId = "CUST-456"
        };

        // Raise the event using the EventRaiser
        var result = await eventRaiser.RaiseEvent(orderEvent);

        // Give some time for the background processing to complete
        await Task.Delay(500);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // 6. Verify the event was successfully raised
        Assert.True(result, "Event should be successfully raised");

        // 7. Verify the SNS client was called to publish the message
        mockSnsClient.Verify(
            x => x.PublishBatchAsync(
                It.Is<PublishBatchRequest>(req =>
                    req.TopicArn == "arn:aws:sns:us-east-1:123456789012:order-events-topic"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // 8. Verify that a message was captured
        Assert.NotEmpty(capturedMessages);

        // 9. Verify the captured message contains the correct order details
        var capturedEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(capturedMessages[0]);
        Assert.NotNull(capturedEvent);
        Assert.Equal("ORD-12345", capturedEvent.OrderId);
        Assert.Equal(199.99m, capturedEvent.Amount);
        Assert.Equal("CUST-456", capturedEvent.CustomerId);

        // 10. Verify worker logs confirm the successful publishing
        eventWorkerLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully published")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RegisterEventChannel_RaiseMultipleEvents_BatchesAndPublishesToSns()
    {
        // Arrange
        var eventRaiserLogger = new Mock<ILogger<EventRaiser>>();
        var eventWorkerLogger = new Mock<ILogger<EventChannelWorker<OrderCreatedEvent>>>();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        var capturedBatchRequests = new List<PublishBatchRequest>();

        mockSnsClient
            .Setup(x => x.PublishBatchAsync(It.IsAny<PublishBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishBatchRequest, CancellationToken>((req, _) =>
            {
                capturedBatchRequests.Add(req);
            })
            .ReturnsAsync(new PublishBatchResponse
            {
                Successful = new List<PublishBatchResultEntry>
                {
                        new PublishBatchResultEntry { Id = "0", MessageId = "msg-1" },
                        new PublishBatchResultEntry { Id = "1", MessageId = "msg-2" },
                        new PublishBatchResultEntry { Id = "2", MessageId = "msg-3" }
                },
                Failed = new List<BatchResultErrorEntry>()
            });

        var eventChannel = new EventChannel<OrderCreatedEvent>();
        var workerConfig = new EventChannelWorkerConfig<OrderCreatedEvent>
        {
            EventChannel = eventChannel,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:order-events-topic",
            SnsClient = mockSnsClient.Object,
            MaxRetryAttempts = 3
        };

        var worker = new EventChannelWorker<OrderCreatedEvent>(workerConfig, eventWorkerLogger.Object);
        var eventRaiser = new EventRaiser(new List<IEventChannel>(), eventRaiserLogger.Object);
        eventRaiser.RegisterChannel(eventChannel);

        await worker.StartAsync(CancellationToken.None);

        // Act
        // Create and raise multiple events
        var orderEvents = new List<OrderCreatedEvent>
            {
                new OrderCreatedEvent { OrderId = "ORD-1001", Amount = 99.99m, CustomerId = "CUST-A" },
                new OrderCreatedEvent { OrderId = "ORD-1002", Amount = 149.99m, CustomerId = "CUST-B" },
                new OrderCreatedEvent { OrderId = "ORD-1003", Amount = 199.99m, CustomerId = "CUST-C" }
            };

        // Raise multiple events using the EventRaiser
        await eventRaiser.RaiseEvents(orderEvents);

        // Give time for background processing
        await Task.Delay(500);

        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Verify the SNS client was called at least once
        mockSnsClient.Verify(
            x => x.PublishBatchAsync(
                It.IsAny<PublishBatchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Verify that batch requests were captured
        Assert.NotEmpty(capturedBatchRequests);

        // Count the total number of entries across all batch requests
        int totalEntries = 0;
        foreach (var request in capturedBatchRequests)
        {
            totalEntries += request.PublishBatchRequestEntries.Count;
        }

        // Verify all 3 events were published
        Assert.Equal(3, totalEntries);

        // Verify the worker logged success
        eventWorkerLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully published")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}