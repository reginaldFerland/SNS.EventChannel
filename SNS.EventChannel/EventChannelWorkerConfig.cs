using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Polly.Retry;

namespace SNS.EventChannel;

/// <summary>
/// Configuration class for EventChannelWorker. Defines the settings required
/// to publish events to an Amazon SNS topic.
/// </summary>
/// <typeparam name="T">The type of events being processed by the worker</typeparam>
public class EventChannelWorkerConfig<T>
{
    /// <summary>
    /// The event channel that the worker will monitor for events to publish.
    /// </summary>
    public required EventChannel<T> EventChannel { get; init; }

    /// <summary>
    /// The Amazon SNS Topic ARN where events will be published.
    /// </summary>
    public required string TopicArn { get; init; }

    /// <summary>
    /// Maximum number of retry attempts for failed publish operations.
    /// Defaults to 3 attempts.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Optional custom Amazon SNS client. If not provided, a default client will be created.
    /// </summary>
    public IAmazonSimpleNotificationService? SnsClient { get; init; }

    /// <summary>
    /// Optional resiliency policy to handle transient failures when publishing to SNS.
    /// Uses Polly for implementing retry strategies.
    /// </summary>
    public AsyncRetryPolicy<PublishBatchResponse>? ResiliencyPolicy { get; init; }
}