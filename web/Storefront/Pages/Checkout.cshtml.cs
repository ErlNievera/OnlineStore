using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class CheckoutModel : PageModel
{
    private readonly OrderApiClient _orderApiClient;
    private readonly PaymentApiClient _paymentApiClient;

    public CheckoutModel(
        OrderApiClient orderApiClient,
        PaymentApiClient paymentApiClient)
    {
        _orderApiClient = orderApiClient;
        _paymentApiClient = paymentApiClient;
    }

    [BindProperty]
    public CreateOrderRequest OrderRequest { get; set; } = new();

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty(SupportsGet = true)]
    public Guid ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ProductName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public decimal Price { get; set; }

    public string Message { get; private set; } = string.Empty;

    public void OnGet()
    {
        OrderRequest.ProductId = ProductId;
        OrderRequest.Quantity = 1;
        OrderRequest.TotalAmount = Price;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        OrderRequest.TotalAmount = Price * OrderRequest.Quantity;

        ModelState.Remove("OrderRequest.TotalAmount");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            OrderResultDto orderResult =
                await _orderApiClient.CreateOrderAsync(OrderRequest);

            CreatePaymentRequest paymentRequest = new CreatePaymentRequest
            {
                OrderId = orderResult.Id,
                Amount = OrderRequest.TotalAmount,
                PaymentMethod = PaymentMethod
            };

            PaymentResultDto paymentResult =
                await _paymentApiClient.CreatePaymentAsync(paymentRequest);

            Message =
                $"Order created successfully. " +
                $"Order ID: {orderResult.Id}, " +
                $"Payment ID: {paymentResult.Id}, " +
                $"Payment Status: {paymentResult.Status}";

            return Page();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
            return Page();
        }
    }
}
