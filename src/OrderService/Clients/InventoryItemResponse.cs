namespace OrderService.Clients
{
    public class InventoryItemResponse
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public int Quantity { get; set; }

        public int ReservedQuantity { get; set; }

        public int AvailableQuantity { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

    }
}
