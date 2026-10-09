
namespace OrderService.Models;

public class Order
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
