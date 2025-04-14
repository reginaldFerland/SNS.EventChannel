using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Polly.Retry;

namespace SNS.EventChannel;

public class EventChannelWorkerConfig<T>
{
    public required EventChannel<T> EventChannel { get; init; }
    public required string TopicArn { get; init; }
    public int MaxRetryAttempts { get; init; } = 3;
    public IAmazonSimpleNotificationService? SnsClient { get; init; }
    public AsyncRetryPolicy<PublishBatchResponse>? ResiliencyPolicy { get; init; }
}