namespace InventoryService.DTOs;

public class CreateInventoryItemRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}
