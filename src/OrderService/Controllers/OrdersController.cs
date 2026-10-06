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
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public OrdersController(
        OrderDbContext db,
        CatalogClient catalogClient,
        RabbitMqPublisher rabbitMqPublisher)
    {
        _db = db;
        _catalogClient = catalogClient;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    // GET: /orders/v1/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders()
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Select(o => new OrderResponse
            {
                Id = o.Id,
                ProductId = o.ProductId,
                CustomerName = o.CustomerName,
                CustomerEmail = o.CustomerEmail,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orders);
    }

    // GET: /orders/v1/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(Guid id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

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
        var product = await _catalogClient.GetProductAsync(
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

        if (!product.IsActive)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Product is inactive",
                Detail = $"Product '{request.ProductId}' is not currently active."
            });
        }

        // 2. Create the order
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            TotalAmount = request.TotalAmount,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Orders.Add(order);

        // 3. Save the order
        await _db.SaveChangesAsync(cancellationToken);

        // 4. Get or create correlation ID
        var correlationId =
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // 5. Create OrderPlaced event
        var orderPlaced = new OrderPlaced(
            order.Id,
            order.ProductId,
            1,
            order.TotalAmount,
            now,
            correlationId);

        // 6. Publish event to RabbitMQ
        await _rabbitMqPublisher.PublishOrderPlacedAsync(
            orderPlaced,
            cancellationToken);

        // 7. Return created order
        var response = ToResponse(order);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            response);
    }

    // PUT: /orders/v1/orders/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> UpdateOrder(
        Guid id,
        UpdateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

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
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

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
