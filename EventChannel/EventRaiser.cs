using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventChannelLib;

/// <summary>
/// Class for raising events to be published to SNS via event channels
/// </summary>
public class EventRaiser
{
    private readonly Dictionary<Type, object> _eventChannels = [];
    private readonly ILogger<EventRaiser> _logger;

    /// <summary>
    /// Creates a new instance of EventRaiser
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve event channels</param>
    /// <param name="logger">Logger for the EventRaiser</param>
    public EventRaiser(IServiceProvider serviceProvider, ILogger<EventRaiser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Find all registered EventChannel<T> services
        var channelType = typeof(EventChannel<>);
        var registeredServices = serviceProvider.GetServices<object>()
            .Where(service => service != null &&
                  service.GetType().IsGenericType &&
                  service.GetType().GetGenericTypeDefinition() == channelType);

        foreach (var service in registeredServices)
        {
            var genericType = service.GetType().GetGenericArguments()[0];
            _eventChannels[genericType] = service;
            _logger.LogInformation("Auto-registered event channel for type {EventType}", genericType.Name);
        }
    }

    /// <summary>
    /// Registers an event channel for a specific type
    /// </summary>
    /// <typeparam name="T">The type of events the channel handles</typeparam>
    /// <param name="channel">The channel to register</param>
    public void RegisterChannel<T>(EventChannel<T> channel)
    {
        var eventType = typeof(T);
        _eventChannels[eventType] = channel;
        _logger.LogInformation("Registered event channel for type {EventType}", eventType.Name);
    }

    /// <summary>
    /// Raises an event to be published to SNS via the appropriate channel
    /// </summary>
    /// <typeparam name="T">The type of event to raise</typeparam>
    /// <param name="event">The event to raise</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>True if the event was successfully added to the channel, false otherwise</returns>
    public async Task<bool> RaiseEvent<T>(T @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        var eventType = typeof(T);

        if (!_eventChannels.TryGetValue(eventType, out var channelObj))
        {
            _logger.LogWarning("No event channel configured for type {EventType}", eventType.Name);
            return false;
        }

        if (channelObj is EventChannel<T> typedChannel)
        {
            return await typedChannel.WriteAsync(@event, cancellationToken);
        }

        _logger.LogError("Channel found for type {EventType} but could not cast to EventChannel<{EventType}>",
            eventType.Name, eventType.Name);
        return false;
    }

    /// <summary>
    /// Raises multiple events to be published to SNS via the appropriate channel
    /// </summary>
    /// <typeparam name="T">The type of events to raise</typeparam>
    /// <param name="events">The events to raise</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that completes when all events have been added to the channel</returns>
    public async Task RaiseEvents<T>(IEnumerable<T> events, CancellationToken cancellationToken = default)
    {
        var eventType = typeof(T);

        if (!_eventChannels.TryGetValue(eventType, out var channelObj))
        {
            _logger.LogWarning("No event channel configured for type {EventType}", eventType.Name);
            return;
        }

        if (channelObj is EventChannel<T> typedChannel)
        {
            await typedChannel.WriteAllAsync(events, cancellationToken);
            return;
        }

        _logger.LogError("Channel found for type {EventType} but could not cast to EventChannel<{EventType}>",
            eventType.Name, eventType.Name);
    }
}