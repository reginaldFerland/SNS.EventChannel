using System;
using System.Threading.Tasks;
using EventChannelLib;
using EventExampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventExampleApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly EventRaiser _eventRaiser;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(EventRaiser eventRaiser, ILogger<OrdersController> logger)
    {
        _eventRaiser = eventRaiser;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
    {
        // Simulate creating an order in the database
        var orderId = Guid.NewGuid().ToString();

        _logger.LogInformation("Creating order {OrderId} for customer {CustomerId}",
            orderId, request.CustomerId);

        // Create and raise the event
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = orderId,
            CustomerId = request.CustomerId,
            TotalAmount = request.TotalAmount,
            ItemCount = request.ItemCount
        };

        var success = await _eventRaiser.RaiseEvent(orderCreatedEvent);

        if (!success)
        {
            _logger.LogWarning("Failed to raise OrderCreatedEvent for order {OrderId}", orderId);
        }

        return Ok(new { OrderId = orderId, Message = "Order created successfully" });
    }
}

public class OrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}
