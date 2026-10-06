using System.Text.Json;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace InventoryService.Consumers;

public class OrderPlacedConsumer
{
    private readonly InventoryDbContext _db;

    public OrderPlacedConsumer(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var orderPlaced = JsonSerializer.Deserialize<OrderPlaced>(message);

        if (orderPlaced is null)
        {
            throw new InvalidOperationException(
                "Invalid OrderPlaced event.");
        }

        // Check if this event was already processed
        var alreadyProcessed = await _db.ProcessedEvents
            .AnyAsync(
                x => x.EventId == orderPlaced.OrderId,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        // Find inventory for the product
        var inventory = await _db.InventoryItems
            .FirstOrDefaultAsync(
                x => x.ProductId == orderPlaced.ProductId,
                cancellationToken);

        if (inventory is null)
        {
            throw new InvalidOperationException(
                $"No inventory item found for product '{orderPlaced.ProductId}'.");
        }

        // Check available stock
        if (inventory.AvailableQuantity < orderPlaced.Quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for product '{orderPlaced.ProductId}'.");
        }

        // Reserve stock
        inventory.ReservedQuantity += orderPlaced.Quantity;

        // Available = Quantity - Reserved
        inventory.UpdatedAt = DateTime.UtcNow;

        // Remember that this event was processed
        _db.ProcessedEvents.Add(new Models.ProcessedEvent
        {
            Id = Guid.NewGuid(),
            EventId = orderPlaced.OrderId,
            ProcessedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
