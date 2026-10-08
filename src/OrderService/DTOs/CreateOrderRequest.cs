using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class CreateOrderRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Quantity { get; set; }
}