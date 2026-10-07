using System.ComponentModel.DataAnnotations;

namespace Storefront.Models;

public class CreateOrderRequest
{
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0.")]
    public decimal TotalAmount { get; set; }
}