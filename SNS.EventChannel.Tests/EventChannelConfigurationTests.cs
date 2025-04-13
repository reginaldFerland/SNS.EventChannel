using System;
using Amazon;
using Xunit;
using SNS.EventChannel;

namespace SNS.EventChannel.Tests;

public class EventChannelConfigurationTests
{
    [Fact]
    public void AddChannel_ShouldAddNewChannelToCollection()
    {
        // Arrange
        var config = new EventChannelConfiguration();
        string topicArn = "arn:aws:sns:us-east-1:123456789012:MyTopic";

        // Act
        config.AddChannel<TestEventType>(topicArn);

        // Assert
        Assert.Single(config.Channels);
        Assert.Equal(topicArn, config.Channels[0].TopicArn);
        Assert.Equal(typeof(TestEventType), config.Channels[0].EventType);
        Assert.Equal(3, config.Channels[0].MaxRetryAttempts); // Default value
        Assert.Equal(1_000_000, config.Channels[0].BoundedCapacity); // Default value
    }

    [Fact]
    public void AddChannel_WithCustomParameters_ShouldSetCorrectValues()
    {
        // Arrange
        var config = new EventChannelConfiguration();
        string topicArn = "arn:aws:sns:us-east-1:123456789012:MyTopic";
        int maxRetries = 5;
        int capacity = 500;

        // Act
        config.AddChannel<TestEventType>(topicArn, maxRetries, capacity);

        // Assert
        Assert.Single(config.Channels);
        var channel = config.Channels[0];
        Assert.Equal(topicArn, channel.TopicArn);
        Assert.Equal(typeof(TestEventType), channel.EventType);
        Assert.Equal(maxRetries, channel.MaxRetryAttempts);
        Assert.Equal(capacity, channel.BoundedCapacity);
    }

    [Fact]
    public void AddChannel_MultipleCalls_ShouldAddMultipleChannels()
    {
        // Arrange
        var config = new EventChannelConfiguration();

        // Act
        config
            .AddChannel<TestEventType>("arn:aws:sns:us-east-1:123456789012:Topic1")
            .AddChannel<AnotherTestEventType>("arn:aws:sns:us-east-1:123456789012:Topic2");

        // Assert
        Assert.Equal(2, config.Channels.Count);
        Assert.Equal(typeof(TestEventType), config.Channels[0].EventType);
        Assert.Equal(typeof(AnotherTestEventType), config.Channels[1].EventType);
    }

    [Fact]
    public void EventChannelConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var channelConfig = new EventChannelConfig();

        // Assert
        Assert.Equal(string.Empty, channelConfig.TopicArn);
        Assert.Equal(3, channelConfig.MaxRetryAttempts);
        Assert.Equal(1_000_000, channelConfig.BoundedCapacity);
        Assert.Equal("us-east-1", channelConfig.RegionName);
        Assert.Equal(RegionEndpoint.USEast1, channelConfig.RegionEndpoint);
        Assert.Equal(string.Empty, channelConfig.ServiceUrl);
    }

    // Sample event types for testing
    private class TestEventType { }
    private class AnotherTestEventType { }
}
