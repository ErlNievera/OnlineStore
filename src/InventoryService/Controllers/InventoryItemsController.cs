using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("inventory/v1/items")]
[Produces("application/json")]
public class InventoryItemsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public InventoryItemsController(InventoryDbContext db)
    {
        _db = db;
    }

    // GET: /inventory/v1/items
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> GetItems()
    {
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Select(item => ToResponse(item))
            .ToListAsync();

        return Ok(items);
    }

    // GET: /inventory/v1/items/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryItemResponse>> GetItem(Guid id)
    {
        var item = await _db.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory item not found",
                Detail = $"No inventory item was found with ID '{id}'."
            });
        }

        return Ok(ToResponse(item));
    }

    // POST: /inventory/v1/items
    [HttpPost]
    public async Task<ActionResult<InventoryItemResponse>> CreateItem(
        CreateInventoryItemRequest request)
    {
        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid product ID",
                Detail = "ProductId must be a valid GUID."
            });
        }

        if (request.Quantity < 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid quantity",
                Detail = "Quantity cannot be negative."
            });
        }

        var now = DateTime.UtcNow;

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            ReservedQuantity = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        var response = ToResponse(item);

        return CreatedAtAction(
            nameof(GetItem),
            new { id = item.Id },
            response);
    }

    // PUT: /inventory/v1/items/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InventoryItemResponse>> UpdateItem(
        Guid id,
        UpdateInventoryItemRequest request)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory item not found",
                Detail = $"No inventory item was found with ID '{id}'."
            });
        }

        if (request.Quantity < 0 || request.ReservedQuantity < 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid inventory quantity",
                Detail = "Quantity and ReservedQuantity cannot be negative."
            });
        }

        if (request.ReservedQuantity > request.Quantity)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid reservation",
                Detail = "ReservedQuantity cannot exceed Quantity."
            });
        }

        item.Quantity = request.Quantity;
        item.ReservedQuantity = request.ReservedQuantity;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(item));
    }

    // POST: /inventory/v1/items/{id}/reserve
    [HttpPost("{id:guid}/reserve")]
    public async Task<ActionResult<InventoryItemResponse>> ReserveStock(
        Guid id,
        ReserveInventoryRequest request)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory item not found",
                Detail = $"No inventory item was found with ID '{id}'."
            });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid quantity",
                Detail = "Reservation quantity must be greater than 0."
            });
        }

        if (item.AvailableQuantity < request.Quantity)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Insufficient stock",
                Detail = $"Only {item.AvailableQuantity} item(s) are available."
            });
        }

        item.ReservedQuantity += request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(item));
    }

    // POST: /inventory/v1/items/{id}/release
    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<InventoryItemResponse>> ReleaseStock(
        Guid id,
        ReleaseInventoryRequest request)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory item not found",
                Detail = $"No inventory item was found with ID '{id}'."
            });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid quantity",
                Detail = "Release quantity must be greater than 0."
            });
        }

        if (request.Quantity > item.ReservedQuantity)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid release",
                Detail = $"Cannot release {request.Quantity} item(s). Only {item.ReservedQuantity} item(s) are currently reserved."
            });
        }

        item.ReservedQuantity -= request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(item));
    }

    // DELETE: /inventory/v1/items/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Inventory item not found",
                Detail = $"No inventory item was found with ID '{id}'."
            });
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static InventoryItemResponse ToResponse(InventoryItem item)
    {
        return new InventoryItemResponse
        {
            Id = item.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            ReservedQuantity = item.ReservedQuantity,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
