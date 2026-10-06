namespace PaymentService.DTOs
{
    public class UpdatePaymentRequest
    {

        public decimal Amount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;
    }
}
