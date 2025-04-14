using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace SNS.EventChannel;

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
        EventChannelConfig<T> options) where T : class
    {

        // Register the channel as singleton
        services.AddSingleton(sp =>
            new EventChannel<T>(options.UseBoundedCapacity, options.BoundedCapacity)
        );
        services.AddSingleton<IEventChannel>(sp =>
            sp.GetRequiredService<EventChannel<T>>());

        // Register the worker as a hosted service with config
        services.AddHostedService(sp =>
            new EventChannelWorker<T>(
                options.WorkerConfig,
                sp.GetRequiredService<ILogger<EventChannelWorker<T>>>()
                ));

        return services;
    }
}
