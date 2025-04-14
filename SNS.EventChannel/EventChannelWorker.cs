using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace SNS.EventChannel;

/// <summary>
/// A worker that processes items from an EventChannel and publishes them to SNS
/// with retry and rate limiting capabilities
/// </summary>
/// <typeparam name="T">The type of items that will be processed by this worker</typeparam>
public class EventChannelWorker<T> : IHostedService
    where T : class
{
    private readonly EventChannel<T> _eventChannel;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly string _topicArn;
    private readonly AsyncRetryPolicy<PublishBatchResponse> _resiliencyPolicy;
    private const int MaxBatchSize = 10;
    private readonly ILogger<EventChannelWorker<T>> _logger;

    /// <summary>
    /// Creates a new instance of EventChannelWorker
    /// </summary>
    /// <param name="eventChannel">The event channel to process items from</param>
    /// <param name="snsClient">The SNS client used to publish messages</param>
    /// <param name="topicArn">The SNS topic ARN to publish to</param>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts for publishing</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public EventChannelWorker(
        EventChannelWorkerConfig<T> config,
        ILogger<EventChannelWorker<T>> logger)
    {
        _topicArn = config.TopicArn ?? throw new ArgumentNullException(nameof(config));
        _snsClient = config.SnsClient ?? throw new ArgumentNullException(nameof(config));
        _eventChannel = config.EventChannel ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create resilience policy combining retry and rate limiting
        _resiliencyPolicy = config.ResiliencyPolicy ?? CreateResiliencyPolicy(config.MaxRetryAttempts);

        _logger.LogInformation("EventChannelWorker initialized with topic ARN: {TopicArn}", _topicArn);
    }

    /// <summary>
    /// Creates the resilience policy for SNS publishing
    /// </summary>
    private AsyncRetryPolicy<PublishBatchResponse> CreateResiliencyPolicy(int maxRetryAttempts)
    {
        // Create a retry policy for transient errors
        var retryPolicy = Policy<PublishBatchResponse>
            .Handle<ThrottledException>()
            .Or<InternalErrorException>()
            .Or<Amazon.Runtime.AmazonServiceException>(ex =>
                ex.StatusCode == System.Net.HttpStatusCode.InternalServerError
                || ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
            )
            .WaitAndRetryAsync(
                maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "Retrying SNS publish after {Delay}ms, attempt {RetryCount} due to {Exception}",
                        timespan.TotalMilliseconds,
                        retryAttempt,
                        outcome.Exception?.Message);
                });

        return retryPolicy;
    }

    /// <summary>
    /// Process items from the channel
    /// </summary>
    private async Task ProcessChannelAsync(CancellationToken cancellationToken)
    {
        var reader = _eventChannel.GetReader();
        var batchBuffer = new List<T>(MaxBatchSize);

        try
        {
            _logger.LogInformation("Starting to process items from channel");
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                // Read up to MaxBatchSize items
                while (reader.TryRead(out var item))
                {
                    batchBuffer.Add(item);

                    // When we have max batch size or no more items are available, process the batch
                    if (batchBuffer.Count == MaxBatchSize || !reader.TryPeek(out _))
                    {
                        if (batchBuffer.Count > 0)
                        {
                            // Process the batch
                            _logger.LogDebug("Processing batch of {Count} items", batchBuffer.Count);
                            await ProcessBatchAsync(batchBuffer, cancellationToken);
                            batchBuffer.Clear();
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Channel processing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing channel: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Process a batch of items
    /// </summary>
    private async Task ProcessBatchAsync(List<T> items, CancellationToken cancellationToken)
    {
        var batches = items.Chunk(MaxBatchSize).ToList()
            .Select(async batch => await PublishBatchToSnsAsync([.. batch], cancellationToken));

        await Task.WhenAll(batches);
    }

    /// <summary>
    /// Publish a batch of items to SNS using batch API
    /// </summary>
    private async Task PublishBatchToSnsAsync(List<T> items, CancellationToken cancellationToken)
    {
        try
        {
            var publishBatchRequest = new PublishBatchRequest
            {
                TopicArn = _topicArn,
                PublishBatchRequestEntries = items
                    .Select((item, index) => new PublishBatchRequestEntry
                    {
                        Id = index.ToString(),  // Adding required Id field
                        Message = SerializeMessage(item)
                    })
                    .ToList()
            };

            // Use the resilience policy to handle retries and rate limiting
            var response = await _resiliencyPolicy.ExecuteAsync(
                async (ct) => await _snsClient.PublishBatchAsync(publishBatchRequest, ct),
                cancellationToken
            );

            // Log successful publishes
            if (response.Successful.Count > 0)
            {
                _logger.LogDebug("Successfully published {Count} messages to SNS", response.Successful.Count);
            }

            // Handle failed publishes
            if (response.Failed.Count > 0)
            {
                foreach (var failedEntry in response.Failed)
                {
                    _logger.LogError("Failed to publish message {MessageId}: {Code} - {Message}",
                        failedEntry.Id, failedEntry.Code, failedEntry.Message);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to publish batch to SNS: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Serializes a message to JSON format
    /// </summary>
    /// <param name="item">The item to serialize</param>
    /// <returns>JSON string representation of the item</returns>
    private string SerializeMessage(T item)
    {
        try
        {
            return JsonSerializer.Serialize(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize message: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Failed to serialize message of type {typeof(T).Name}", ex);
        }
    }

    private Task? _processingTask;
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Starts the worker as a hosted service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EventChannelWorker hosted service");

        // Create a linked token source that will be cancelled when the application stops
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start processing in the background (not awaited)
        _processingTask = ProcessChannelAsync(_stoppingCts.Token);

        // Return completed task to allow startup to continue
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the worker gracefully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EventChannelWorker hosted service");

        // Cancel our processing loop
        if (_stoppingCts != null)
        {
            _stoppingCts.Cancel();
            _stoppingCts.Dispose();
        }

        // Wait for the processing task to complete with a timeout
        if (_processingTask != null)
        {
            try
            {
                await Task.WhenAny(_processingTask, Task.Delay(5000, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping worker: {ErrorMessage}", ex.Message);
            }
        }
    }
}