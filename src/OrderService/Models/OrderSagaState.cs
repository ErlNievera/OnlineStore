namespace OrderService.Models;

public class OrderSagaState
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "AwaitingInventory";

    public string CorrelationId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}