using System;

namespace EventExampleApi.Models;

/// <summary>
/// Sample event that represents a new order being created
/// </summary>
public class OrderCreatedEvent2
{
    /// <summary>
    /// The unique identifier for the order
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The customer ID associated with the order
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// The total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The number of items in the order
    /// </summary>
    public int ItemCount { get; set; }
}
