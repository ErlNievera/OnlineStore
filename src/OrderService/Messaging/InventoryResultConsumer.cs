using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Data;
using OrderService.Models;
using Shared.Events;

namespace OrderService.Messaging;

public class InventoryResultConsumer
{
    private readonly OrderDbContext _db;
    private readonly PaymentClient _paymentClient;
    private readonly InventoryClient _inventoryClient;
    private readonly ILogger<InventoryResultConsumer> _logger;

    public InventoryResultConsumer(
        OrderDbContext db,
        PaymentClient paymentClient,
        InventoryClient inventoryClient,
        ILogger<InventoryResultConsumer> logger)
    {
        _db = db;
        _paymentClient = paymentClient;
        _inventoryClient = inventoryClient;
        _logger = logger;
    }

    public async Task HandleReservedAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        InventoryReserved? inventoryReserved =
            JsonSerializer.Deserialize<InventoryReserved>(message);

        if (inventoryReserved is null ||
            inventoryReserved.OrderId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Invalid InventoryReserved event.");
        }

        OrderSagaState? saga = await _db.OrderSagaStates
            .FirstOrDefaultAsync(
                x => x.OrderId == inventoryReserved.OrderId,
                cancellationToken);

        if (saga is null)
        {
            throw new InvalidOperationException(
                $"Saga state not found for order '{inventoryReserved.OrderId}'.");
        }

        // Ignore duplicate events once this Saga has moved forward.
        if (saga.Status != "AwaitingInventory")
        {
            _logger.LogInformation(
                "Ignoring inventory confirmation for order {OrderId} with saga status {Status}.",
                saga.OrderId,
                saga.Status);

            return;
        }

        Order? order = await _db.Orders.FirstOrDefaultAsync(
            x => x.Id == inventoryReserved.OrderId,
            cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException(
                $"Order '{inventoryReserved.OrderId}' was not found.");
        }

        saga.Status = "ProcessingPayment";
        saga.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            PaymentResponse payment = await _paymentClient.CreatePaymentAsync(
                new CreatePaymentRequest
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    PaymentMethod = "GCash",
                    SimulateFailure = false
                },
                cancellationToken);

            if (string.Equals(
                payment.Status,
                "Succeeded",
                StringComparison.OrdinalIgnoreCase))
            {
                saga.Status = "Completed";
                saga.UpdatedAt = DateTime.UtcNow;

                order.Status = "Completed";
                order.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Payment succeeded and order {OrderId} was completed.",
                    order.Id);

                return;
            }

            await CompensateAsync(
                saga,
                order,
                inventoryReserved.ProductId,
                inventoryReserved.Quantity,
                $"Payment returned status '{payment.Status}'.",
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Payment processing failed for order {OrderId}.",
                order.Id);

            await CompensateAsync(
                saga,
                order,
                inventoryReserved.ProductId,
                inventoryReserved.Quantity,
                "Payment processing could not be completed.",
                cancellationToken);
        }
    }

    public async Task HandleReservationFailedAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        InventoryReservationFailed? inventoryFailed =
            JsonSerializer.Deserialize<InventoryReservationFailed>(message);

        if (inventoryFailed is null ||
            inventoryFailed.OrderId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Invalid InventoryReservationFailed event.");
        }

        OrderSagaState? saga = await _db.OrderSagaStates
            .FirstOrDefaultAsync(
                x => x.OrderId == inventoryFailed.OrderId,
                cancellationToken);

        if (saga is null)
        {
            throw new InvalidOperationException(
                $"Saga state not found for order '{inventoryFailed.OrderId}'.");
        }

        if (saga.Status is "Completed" or "Failed")
        {
            return;
        }

        Order? order = await _db.Orders.FirstOrDefaultAsync(
            x => x.Id == inventoryFailed.OrderId,
            cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException(
                $"Order '{inventoryFailed.OrderId}' was not found.");
        }

        saga.Status = "Failed";
        saga.UpdatedAt = DateTime.UtcNow;

        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Order {OrderId} was cancelled because inventory reservation failed: {Reason}",
            order.Id,
            inventoryFailed.Reason);
    }

    private async Task CompensateAsync(
        OrderSagaState saga,
        Order order,
        Guid productId,
        int quantity,
        string reason,
        CancellationToken cancellationToken)
    {
        InventoryItemResponse? inventory =
            await _inventoryClient.GetInventoryByProductIdAsync(
                productId,
                cancellationToken);

        if (inventory is null)
        {
            throw new InvalidOperationException(
                $"Cannot release stock: inventory for product '{productId}' was not found.");
        }

        bool released = await _inventoryClient.ReleaseStockAsync(
            inventory.Id,
            quantity,
            cancellationToken);

        if (!released)
        {
            throw new InvalidOperationException(
                $"Could not release reserved stock for order '{order.Id}'.");
        }

        saga.Status = "Failed";
        saga.UpdatedAt = DateTime.UtcNow;

        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Order {OrderId} was cancelled and stock was released. Reason: {Reason}",
            order.Id,
            reason);
    }
}
