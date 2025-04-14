# SNS.EventChannel Library

[![NuGet](https://img.shields.io/nuget/v/SNS.EventChannel.svg)](https://www.nuget.org/packages/SNS.EventChannel/)
[![Build Status](https://github.com/reginaldFerland/SNS.EventChannel/actions/workflows/build.yml/badge.svg)](https://github.com/reginaldFerland/SNS.EventChannel/actions)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)

## Overview

SNS.EventChannel is a lightweight, high-performance .NET library that provides a robust event channel system for publishing to SNS asyncrounsly with retry logic in your applications. It enables loose coupling between components through asynchronous event-driven communication.

## Features

- 🚀 High performance, minimal overhead event dispatching
- 🔄 Asynchronous event handling
- 🧩 Strong typing for events with generic support
- 🛡️ Thread-safe event publication

## TODO
- 🔌 Pluggable architecture for custom extensions
- 📊 Built-in support for monitoring and metrics

## Installation

### Package Manager Console

```
Install-Package SNS.EventChannel
```

### .NET CLI

```
dotnet add package SNS.EventChannel
```

## Quick Start

### Install dependencies
```
dotnet add package 
dotnet add package polly
```

### Define Your Event

```csharp
public class OrderCreatedEvent
{
    public string OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Amount { get; set; }
}
```

### Register new EventChannel in startup
```csharp
builder.Services.AddEventChannel<OrderCreatedEvent>(options =>
    {
        options.TopicArn = "EventChannel:TopicArn";
        options.MaxRetryAttempts = 3;
        options.BoundedCapacity = 1_000_000;
        options.UseBoundedCapacity = true; // Set to false for unbounded channel
    });
```

### Register EventRaiser in starup
```csharp
    builder.Services.AddEventRaiser();
```

### Publish Events

```csharp
// Publish an OrderCreatedEvent
await eventChannel.PublishAsync(new OrderCreatedEvent
{
    OrderId = "ORD-12345",
    CreatedAt = DateTime.UtcNow,
    Amount = 99.99m
});
```

## Advanced Usage

### Configuring Event Channels

```csharp
var options = new EventChannelOptions
{
    MaxConcurrentHandlers = 5,
    DefaultTimeout = TimeSpan.FromSeconds(30),
    EnableLogging = true
};

IEventChannel eventChannel = new EventChannel(options);
```

## Performance Considerations

SNS.EventChannel is designed with performance in mind:

- Minimal allocations during event dispatch
- Support for batched event publishing

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.
