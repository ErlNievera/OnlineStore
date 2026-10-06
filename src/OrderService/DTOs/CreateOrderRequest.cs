namespace OrderService.DTOs;

public class CreateOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public decimal TotalAmount { get; set; }
}
