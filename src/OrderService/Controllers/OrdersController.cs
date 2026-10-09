
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
        CatalogProductResponse? product;

        try
        {
            product = await _catalogClient.GetProductAsync(
                request.ProductId,
                cancellationToken);
        }
        catch (DownstreamServiceException ex)
        {
            return CreateDownstreamError(ex);
        }

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
        InventoryItemResponse? inventory;

        try
        {
            inventory = await _inventoryClient.GetInventoryByProductIdAsync(
                request.ProductId,
                cancellationToken);
        }
        catch (DownstreamServiceException ex)
        {
            return CreateDownstreamError(ex);
        }

        if (inventory is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory not found",
                Detail =
                    $"No inventory item was found for product '{request.ProductId}'."
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

        try
        {
            await _rabbitMqPublisher.PublishOrderPlacedAsync(
                orderPlaced,
                cancellationToken);
        }
        catch (Exception)
        {
            order.Status = "Pending";
            await _db.SaveChangesAsync(cancellationToken);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Order event publishing failed",
                    Detail =
                        "The order was saved, but the event could not be published. Check RabbitMQ and try again."
                });
        }

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

    // Convert downstream failures to appropriate HTTP status codes.
    private ObjectResult CreateDownstreamError(
        DownstreamServiceException ex)
    {
        int statusCode = GetDownstreamStatusCode(ex);

        ProblemDetails problem = new ProblemDetails
        {
            Status = statusCode,
            Title = $"{ex.ServiceName} error",
            Detail =
                $"The {ex.ServiceName} could not complete the request. Please try again."
        };

        return StatusCode(statusCode, problem);
    }

    private static int GetDownstreamStatusCode(
        DownstreamServiceException ex)
    {
        if (ex.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            return StatusCodes.Status503ServiceUnavailable;
        }

        if (ex.StatusCode == StatusCodes.Status504GatewayTimeout)
        {
            return StatusCodes.Status504GatewayTimeout;
        }

        if (ex.StatusCode >= 500)
        {
            return StatusCodes.Status502BadGateway;
        }

        return ex.StatusCode;
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
