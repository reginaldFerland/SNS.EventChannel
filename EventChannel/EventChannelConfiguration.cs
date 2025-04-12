using System;
using System.Collections.Generic;
using Amazon;

namespace EventChannelLib;

/// <summary>
/// Configuration for a single event channel
/// </summary>
public class EventChannelConfig
{
    /// <summary>
    /// The SNS topic ARN for this event type
    /// </summary>
    public string TopicArn { get; set; } = string.Empty;

    /// <summary>
    /// The type of events that will be published to this topic
    /// </summary>
    public Type EventType { get; set; } = null!;

    /// <summary>
    /// Maximum number of retry attempts for publishing
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// The maximum number of items that can be stored in the channel
    /// </summary>
    public int BoundedCapacity { get; set; } = 1_000_000;

    /// <summary>
    /// AWS region name (e.g., "us-east-1")
    /// </summary>
    public string RegionName { get; set; }

    /// <summary>
    /// AWS region endpoint
    /// </summary>
    public RegionEndpoint RegionEndpoint { get; set; }

    /// <summary>
    /// Custom service URL for the SNS service
    /// </summary>
    public string ServiceUrl { get; set; }
}

/// <summary>
/// Configuration for event channels and workers
/// </summary>
public class EventChannelConfiguration
{
    /// <summary>
    /// List of channel configurations
    /// </summary>
    public List<EventChannelConfig> Channels { get; set; } = new List<EventChannelConfig>();

    /// <summary>
    /// Adds a new channel configuration
    /// </summary>
    public EventChannelConfiguration AddChannel<T>(string topicArn, int maxRetryAttempts = 3, int boundedCapacity = 1_000_000)
    {
        Channels.Add(new EventChannelConfig
        {
            TopicArn = topicArn,
            EventType = typeof(T),
            MaxRetryAttempts = maxRetryAttempts,
            BoundedCapacity = boundedCapacity
        });

        return this;
    }
}