using System.Text.Json;
using InventoryService.Data;
using InventoryService.Messaging;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace InventoryService.Consumers;

public class OrderPlacedConsumer
{
    private readonly InventoryDbContext _db;
    private readonly InventoryEventPublisher _eventPublisher;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(
        InventoryDbContext db,
        InventoryEventPublisher eventPublisher,
        ILogger<OrderPlacedConsumer> logger)
    {
        _db = db;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task HandleAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        OrderPlaced? orderPlaced =
            JsonSerializer.Deserialize<OrderPlaced>(message);

        if (orderPlaced is null ||
            orderPlaced.OrderId == Guid.Empty ||
            orderPlaced.ProductId == Guid.Empty ||
            orderPlaced.Quantity <= 0)
        {
            throw new InvalidOperationException(
                "Invalid OrderPlaced event.");
        }

        string? failureReason = null;

        await using (var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken))
        {
            bool alreadyProcessed = await _db.ProcessedEvents
                .AnyAsync(
                    x => x.EventId == orderPlaced.OrderId,
                    cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogInformation(
                    "Order event {OrderId} was already processed.",
                    orderPlaced.OrderId);

                await transaction.CommitAsync(cancellationToken);
                return;
            }

            InventoryItem? inventory = await _db.InventoryItems
                .FirstOrDefaultAsync(
                    x => x.ProductId == orderPlaced.ProductId,
                    cancellationToken);

            if (inventory is null)
            {
                failureReason =
                    $"No inventory item found for product '{orderPlaced.ProductId}'.";
            }
            else
            {
                int availableQuantity =
                    inventory.Quantity - inventory.ReservedQuantity;

                if (availableQuantity < orderPlaced.Quantity)
                {
                    failureReason =
                        $"Insufficient stock for product '{orderPlaced.ProductId}'.";
                }
                else
                {
                    inventory.ReservedQuantity += orderPlaced.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;

                    _db.ProcessedEvents.Add(new ProcessedEvent
                    {
                        Id = Guid.NewGuid(),
                        EventId = orderPlaced.OrderId,
                        ProcessedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (failureReason is not null)
        {
            await _eventPublisher.PublishInventoryReservationFailedAsync(
                new InventoryReservationFailed(
                    orderPlaced.OrderId,
                    orderPlaced.ProductId,
                    orderPlaced.Quantity,
                    failureReason,
                    orderPlaced.CorrelationId),
                cancellationToken);

            _logger.LogWarning(
                "Inventory reservation failed for order {OrderId}: {Reason}",
                orderPlaced.OrderId,
                failureReason);

            return;
        }

        await _eventPublisher.PublishInventoryReservedAsync(
            new InventoryReserved(
                orderPlaced.OrderId,
                orderPlaced.ProductId,
                orderPlaced.Quantity,
                orderPlaced.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Reserved {Quantity} units for order {OrderId}.",
            orderPlaced.Quantity,
            orderPlaced.OrderId);
    }
}