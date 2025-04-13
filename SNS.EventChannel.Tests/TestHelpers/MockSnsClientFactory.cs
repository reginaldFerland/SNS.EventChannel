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
}
