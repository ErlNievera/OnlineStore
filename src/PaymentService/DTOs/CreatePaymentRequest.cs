namespace PaymentService.DTOs
{
    public class CreatePaymentRequest
    {
        public Guid OrderId { get; set; }

        public decimal Amount { get; set; }

        public bool SimulateFailure { get; set; } = false;
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
