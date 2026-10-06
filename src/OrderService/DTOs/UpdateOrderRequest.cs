namespace OrderService.DTOs;

public class UpdateOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";
}
