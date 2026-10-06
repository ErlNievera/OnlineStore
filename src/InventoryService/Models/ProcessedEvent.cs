namespace InventoryService.Models;

public class ProcessedEvent
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public DateTime ProcessedAt { get; set; }
}
