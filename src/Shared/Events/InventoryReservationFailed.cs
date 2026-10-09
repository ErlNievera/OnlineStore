namespace Shared.Events;

public record InventoryReservationFailed(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string Reason,
    string CorrelationId);