namespace Shared.Events;

public record InventoryReserved(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string CorrelationId);