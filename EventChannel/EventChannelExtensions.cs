using System;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventChannelLib;

/// <summary>
/// Extension methods for registering and configuring event channels
/// </summary>
public static class EventChannelExtensions
{
    /// <summary>
    /// Adds and configures an event channel system
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>Event channel builder for further configuration</returns>
    public static IServiceCollection AddEventRaiser(this IServiceCollection services)
    {
        // Register the EventRaiser as singleton
        services.TryAddSingleton<EventRaiser>();

        return services;
    }

    /// <summary>
    /// Configures an event channel for the specified event type
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="services">The event channel builder</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The builder for method chaining</returns>
    public static IServiceCollection AddEventChannel<T>(
        this IServiceCollection services,
        Action<EventChannelConfig> configure) where T : class
    {
        var options = new EventChannelConfig();
        configure.Invoke(options);

        // Register the channel as singleton
        services.TryAddSingleton<EventChannel<T>>(sp =>
            new EventChannel<T>(options.BoundedCapacity));

        // Register the worker as a hosted service with config
        services.AddHostedService<EventChannelWorker<T>>(sp =>
            new EventChannelWorker<T>(
                sp.GetRequiredService<EventChannel<T>>(),
                sp.GetRequiredService<AmazonSimpleNotificationServiceClient>(),
                options.TopicArn,
                sp.GetRequiredService<ILogger<EventChannelWorker<T>>>(),
                options.MaxRetryAttempts));

        return services;
    }
}
