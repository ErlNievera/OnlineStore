using System.ComponentModel.DataAnnotations;

namespace CatalogService.DTOs;

public class CreateProductRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}
