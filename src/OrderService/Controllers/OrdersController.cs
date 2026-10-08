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
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly CatalogClient _catalogClient;
    private readonly InventoryClient _inventoryClient;
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public OrdersController(
        OrderDbContext db,
        CatalogClient catalogClient,
        InventoryClient inventoryClient,
        RabbitMqPublisher rabbitMqPublisher)
    {
        _db = db;
        _catalogClient = catalogClient;
        _inventoryClient = inventoryClient;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    // GET: /orders/v1/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders(
        CancellationToken cancellationToken)
    {
        List<Order> orders = await _db.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        List<OrderResponse> response = orders
            .Select(ToResponse)
            .ToList();

        return Ok(response);
    }

    // GET: /orders/v1/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(
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
                Detail = $"No order was found with ID '{id}'."
            });
        }

        return Ok(ToResponse(order));
    }

    // POST: /orders/v1/orders
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validate product through CatalogService
        CatalogProductResponse? product = await _catalogClient.GetProductAsync(
            request.ProductId,
            cancellationToken);

        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"No product was found with ID '{request.ProductId}'."
            });
        }

        // 2. Check if product is active
        if (!product.IsActive)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Product is inactive",
                Detail = $"Product '{request.ProductId}' is not currently active."
            });
        }

        // 3. Validate quantity
        if (request.Quantity <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid quantity",
                Detail = "Quantity must be greater than zero."
            });
        }

        // 4. Find inventory using ProductId
        InventoryItemResponse? inventory =
            await _inventoryClient.GetInventoryByProductIdAsync(
                request.ProductId,
                cancellationToken);

        if (inventory is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory not found",
                Detail = $"No inventory item was found for product '{request.ProductId}'."
            });
        }

        // 5. Check available stock
        if (inventory.AvailableQuantity < request.Quantity)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Insufficient stock",
                Detail =
                    $"Only {inventory.AvailableQuantity} item(s) are currently available."
            });
        }

        // 6. Reserve inventory
        bool stockReserved = await _inventoryClient.ReserveStockAsync(
            inventory.Id,
            request.Quantity,
            cancellationToken);

        if (!stockReserved)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unable to reserve inventory",
                Detail = "The requested inventory could not be reserved."
            });
        }

        // 7. Create order
        DateTime now = DateTime.UtcNow;

        Order order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            TotalAmount = product.Price * request.Quantity,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        // 8. Get or create correlation ID
        string correlationId =
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // 9. Create OrderPlaced event
        OrderPlaced orderPlaced = new OrderPlaced(
            order.Id,
            order.ProductId,
            request.Quantity,
            order.TotalAmount,
            now,
            correlationId);

        // 10. RabbitMQ publishing will be enabled later
        // await _rabbitMqPublisher.PublishOrderPlacedAsync(
        //     orderPlaced,
        //     cancellationToken);

        // 11. Return created order
        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            ToResponse(order));
    }

    // PUT: /orders/v1/orders/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> UpdateOrder(
        Guid id,
        UpdateOrderRequest request,
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
                Detail = $"No order was found with ID '{id}'."
            });
        }

        order.CustomerName = request.CustomerName;
        order.CustomerEmail = request.CustomerEmail;
        order.TotalAmount = request.TotalAmount;
        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(order));
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
                Detail = $"No order was found with ID '{id}'."
            });
        }

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            ProductId = order.ProductId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }
}