namespace Shared.Events;

public record OrderPlaced(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal TotalAmount,
    DateTime OccurredAt,
    string CorrelationId);
