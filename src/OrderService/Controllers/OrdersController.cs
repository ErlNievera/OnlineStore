using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Messaging;
using OrderService.Models;
using Shared.Events;

namespace OrderService.Controllers;

[ApiController]
[Route("orders/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly CatalogClient _catalogClient;
    private readonly InventoryClient _inventoryClient;
    private readonly RabbitMqPublisher _rabbitMqPublisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderDbContext db,
        CatalogClient catalogClient,
        InventoryClient inventoryClient,
        RabbitMqPublisher rabbitMqPublisher,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _catalogClient = catalogClient;
        _inventoryClient = inventoryClient;
        _rabbitMqPublisher = rabbitMqPublisher;
        _logger = logger;
    }

    // GET: /orders/v1/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
        CancellationToken cancellationToken)
    {
        List<Order> orders = await _db.Orders
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    // GET: /orders/v1/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        Order? order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order not found",
                Detail = $"Order '{id}' was not found."
            });
        }

        return Ok(order);
    }

    // POST: /orders/v1/orders
    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid product",
                Detail = "A valid ProductId is required."
            });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid quantity",
                Detail = "Quantity must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid customer",
                Detail = "CustomerName is required."
            });
        }

        // Confirm that the product exists in CatalogService.
        CatalogProductResponse? product =
    await _catalogClient.GetProductAsync(
        request.ProductId,
        cancellationToken);

        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"Product '{request.ProductId}' was not found."
            });
        }

        // Check available inventory before accepting the order.
        // This check does not reserve stock. The RabbitMQ consumer does that.
        InventoryItemResponse? inventory =
            await _inventoryClient.GetInventoryByProductIdAsync(
                request.ProductId,
                cancellationToken);

        if (inventory is null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Inventory unavailable",
                Detail = "No inventory record exists for this product."
            });
        }

        int availableQuantity =
            inventory.Quantity - inventory.ReservedQuantity;

        if (availableQuantity < request.Quantity)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Insufficient inventory",
                Detail = "There is not enough available stock for this order."
            });
        }

        DateTime now = DateTime.UtcNow;

        // Use the catalog price rather than trusting a price from the client.

        Order order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Quantity = request.Quantity,
            TotalAmount = product.Price * request.Quantity,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };


        string correlationId =
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        OrderSagaState saga = new OrderSagaState
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = order.ProductId,
            Quantity = request.Quantity,
            Amount = order.TotalAmount,
            Status = "AwaitingInventory",
            CorrelationId = correlationId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Orders.Add(order);
        _db.OrderSagaStates.Add(saga);

        await _db.SaveChangesAsync(cancellationToken);

        // Publish only after the order and saga state have been saved.
        OrderPlaced orderPlaced = new OrderPlaced(
            order.Id,
            order.ProductId,
            request.Quantity,
            order.TotalAmount,
            now,
            correlationId);

        try
        {
            await _rabbitMqPublisher.PublishOrderPlacedAsync(
                orderPlaced,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not publish OrderPlaced for order {OrderId}.",
                order.Id);

            // The order and saga state remain in the database.
            // An outbox/retry mechanism is needed to publish this event reliably.
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Order accepted but processing has not started",
                    Detail = $"Order '{order.Id}' was saved, but its event could not be published. Contact support before retrying."
                });
        }

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            order);
    }

    // DELETE: /orders/v1/orders/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        Order? order = await _db.Orders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order not found",
                Detail = $"Order '{id}' was not found."
            });
        }

        OrderSagaState? saga = await _db.OrderSagaStates
            .FirstOrDefaultAsync(x => x.OrderId == id, cancellationToken);

        if (saga is not null &&
            saga.Status is "ProcessingPayment" or "Completed")
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Order cannot be deleted",
                Detail = "This order is already being processed or has completed."
            });
        }

        _db.Orders.Remove(order);

        if (saga is not null)
        {
            _db.OrderSagaStates.Remove(saga);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
