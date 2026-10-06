namespace PaymentService.Models
{
    public class Payment
    {
        public Guid Id { get; set; }

        public Guid OrderId { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = "Pending";

        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
