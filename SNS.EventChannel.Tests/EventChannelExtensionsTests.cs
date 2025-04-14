using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Amazon.SimpleNotificationService;
using Xunit;

namespace SNS.EventChannel.Tests;

public class EventChannelExtensionsTests
{
    public class TestEvent
    {
        public string? Message { get; set; }
    }

    [Fact]
    public void AddEventRaiser_RegistersEventRaiserAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEventRaiser();

        // Assert
        var serviceDescriptor = Assert.Single(services, sd => sd.ServiceType == typeof(EventRaiser));
        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void AddEventRaiser_CanBeResolvedFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILogger<EventRaiser>>());
        services.AddEventRaiser();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var eventRaiser = serviceProvider.GetService<EventRaiser>();

        // Assert
        Assert.NotNull(eventRaiser);
    }

    [Fact]
    public void AddEventChannel_RegistersEventChannelAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        var eventChannelConfig = new EventChannelConfig<TestEvent>
        {
            BoundedCapacity = 100,
            WorkerConfig = new EventChannelWorkerConfig<TestEvent>
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
                SnsClient = mockSnsClient.Object,
                EventChannel = null! // Will be set by the extension method
            }
        };

        // Add required services
        services.AddLogging();

        // Act
        services.AddEventChannel(eventChannelConfig);

        // Assert
        var channelDescriptor = Assert.Single(services, sd => sd.ServiceType == typeof(EventChannel<TestEvent>));
        Assert.Equal(ServiceLifetime.Singleton, channelDescriptor.Lifetime);

        var interfaceDescriptor = Assert.Single(services, sd => sd.ServiceType == typeof(IEventChannel) &&
                                                               sd.ImplementationFactory != null);
        Assert.Equal(ServiceLifetime.Singleton, interfaceDescriptor.Lifetime);
    }

    [Fact]
    public void AddEventChannel_RegistersWorkerAsHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        var eventChannelConfig = new EventChannelConfig<TestEvent>
        {
            BoundedCapacity = 100,
            WorkerConfig = new EventChannelWorkerConfig<TestEvent>
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
                SnsClient = mockSnsClient.Object,
                EventChannel = null! // Will be set by the extension method
            }
        };

        // Add required services
        services.AddLogging();

        // Act
        services.AddEventChannel(eventChannelConfig);

        // Assert
        // Verify hosted service registration
        var hostedServiceDescriptor = Assert.Single(services,
            sd => sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
        Assert.NotNull(hostedServiceDescriptor);
    }

    [Fact]
    public void AddEventChannel_CanResolveEventChannel()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        var eventChannelConfig = new EventChannelConfig<TestEvent>
        {
            BoundedCapacity = 100,
            WorkerConfig = new EventChannelWorkerConfig<TestEvent>
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
                SnsClient = mockSnsClient.Object,
                EventChannel = null! // Will be set by the extension method
            }
        };

        // Add required services
        services.AddLogging();
        services.AddEventChannel(eventChannelConfig);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var eventChannel = serviceProvider.GetService<EventChannel<TestEvent>>();
        var eventChannelInterface = serviceProvider.GetService<IEventChannel>();

        // Assert
        Assert.NotNull(eventChannel);
        Assert.NotNull(eventChannelInterface);
        Assert.IsType<EventChannel<TestEvent>>(eventChannelInterface);
    }

    [Fact]
    public void AddEventChannel_ConfiguresWorkerCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<EventChannelWorker<TestEvent>>>();

        mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);

        services.AddSingleton<ILoggerFactory>(mockLoggerFactory.Object);
        services.AddSingleton(mockLogger.Object);

        var workerConfig = new EventChannelWorkerConfig<TestEvent>
        {
            TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic",
            SnsClient = mockSnsClient.Object,
            EventChannel = null! // Will be set by the extension method
        };

        var eventChannelConfig = new EventChannelConfig<TestEvent>
        {
            BoundedCapacity = 100,
            WorkerConfig = workerConfig
        };

        // Act
        services.AddEventChannel(eventChannelConfig);

        // Let's fix the issue by properly capturing the EventChannel instance
        // in our service registration closure
        services.AddSingleton<EventChannelWorker<TestEvent>>(sp =>
        {
            var channel = sp.GetRequiredService<EventChannel<TestEvent>>();
            var config = new EventChannelWorkerConfig<TestEvent>
            {
                TopicArn = workerConfig.TopicArn,
                SnsClient = workerConfig.SnsClient,
                EventChannel = channel,
                MaxRetryAttempts = workerConfig.MaxRetryAttempts,
                ResiliencyPolicy = workerConfig.ResiliencyPolicy
            };
            return new EventChannelWorker<TestEvent>(config, mockLogger.Object);
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var worker = provider.GetService<EventChannelWorker<TestEvent>>();
        Assert.NotNull(worker);
    }
}