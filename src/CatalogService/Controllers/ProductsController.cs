using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Controllers;

[ApiController]
[Route("catalog/v1/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly CatalogDbContext _db;

    public ProductsController(CatalogDbContext db)
    {
        _db = db;
    }

    // GET: /catalog/v1/products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Select(p => new ProductResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(products);
    }

    // GET: /catalog/v1/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(Guid id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"No product was found with ID '{id}'."
            });
        }

        return Ok(ToResponse(product));
    }

    // POST: /catalog/v1/products
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request)
    {
        var now = DateTime.UtcNow;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var response = ToResponse(product);

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.Id },
            response);
    }

    // PUT: /catalog/v1/products/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        Guid id,
        UpdateProductRequest request)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"No product was found with ID '{id}'."
            });
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(product));
    }

    // DELETE: /catalog/v1/products/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"No product was found with ID '{id}'."
            });
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}
