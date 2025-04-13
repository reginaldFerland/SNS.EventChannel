using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Moq;

namespace SNS.EventChannel.Tests.TestHelpers;

/// <summary>
/// Helper class for creating mock SNS clients for testing
/// </summary>
public static class MockSnsClientFactory
{
    /// <summary>
    /// Create a mock IAmazonSimpleNotificationService that returns successful responses
    /// </summary>
    public static IAmazonSimpleNotificationService CreateSuccessfulMockSnsClient()
    {
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        // Mock successful publish response
        mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse
            {
                MessageId = Guid.NewGuid().ToString(),
                HttpStatusCode = System.Net.HttpStatusCode.OK
            });

        return mockSnsClient.Object;
    }

    /// <summary>
    /// Create a mock IAmazonSimpleNotificationService that simulates failures
    /// </summary>
    public static IAmazonSimpleNotificationService CreateFailingMockSnsClient()
    {
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        // Mock failed publish response
        mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Amazon.SimpleNotificationService.Model.NotFoundException("Topic not found"));

        return mockSnsClient.Object;
    }

    /// <summary>
    /// Create a mock IAmazonSimpleNotificationService that times out every other call and succeeds on alternating calls
    /// </summary>
    public static IAmazonSimpleNotificationService CreateAlternatingTimeoutMockSnsClient()
    {
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        var callCounter = 0;

        // Set up mock to alternate between timeout and success
        mockSnsClient
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCounter++;

                if (callCounter % 2 == 1)
                {
                    // Odd calls will time out
                    throw new TaskCanceledException("Request timed out");
                }
                else
                {
                    // Even calls will succeed
                    return Task.FromResult(new PublishResponse
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        HttpStatusCode = System.Net.HttpStatusCode.OK
                    });
                }
            });

        return mockSnsClient.Object;
    }
}
