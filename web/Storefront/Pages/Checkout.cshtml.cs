using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class CheckoutModel : PageModel
{
    private readonly OrderApiClient _orderApiClient;
    private readonly InventoryApiClient _inventoryApiClient;

    public CheckoutModel(
        OrderApiClient orderApiClient,
        InventoryApiClient inventoryApiClient)
    {
        _orderApiClient = orderApiClient;
        _inventoryApiClient = inventoryApiClient;
    }

    [BindProperty(SupportsGet = true)]
    public Guid ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ProductName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public decimal Price { get; set; }

    [BindProperty]
    public string CustomerName { get; set; } = string.Empty;

    [BindProperty]
    public string CustomerEmail { get; set; } = string.Empty;

    [BindProperty]
    public int Quantity { get; set; } = 1;

    public int AvailableQuantity { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadInventoryAsync();

        if (AvailableQuantity <= 0)
        {
            ErrorMessage = "This product is currently out of stock.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadInventoryAsync();

        if (AvailableQuantity <= 0)
        {
            ErrorMessage = "This product is currently out of stock.";
            return Page();
        }

        if (Quantity < 1)
        {
            ErrorMessage = "Quantity must be at least 1.";
            return Page();
        }

        if (Quantity > AvailableQuantity)
        {
            ErrorMessage =
                $"Only {AvailableQuantity} item(s) are available.";

            return Page();
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ErrorMessage = "Customer name is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(CustomerEmail))
        {
            ErrorMessage = "Customer email is required.";
            return Page();
        }

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(CustomerEmail))
        {
            ErrorMessage = "Please enter a valid email address.";
            return Page();
        }

        try
        {
            decimal totalAmount = Price * Quantity;

            CreateOrderRequest request = new CreateOrderRequest
            {
                ProductId = ProductId,
                CustomerName = CustomerName,
                CustomerEmail = CustomerEmail,
                Quantity = Quantity,
                TotalAmount = totalAmount
            };

            OrderResultDto order =
                await _orderApiClient.CreateOrderAsync(request);

            return RedirectToPage(
                "/OrderConfirmation",
                new
                {
                    orderId = order.Id
                });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private async Task LoadInventoryAsync()
    {
        List<InventoryItemDto> inventoryItems =
            await _inventoryApiClient.GetItemsAsync();

        InventoryItemDto? inventory =
            inventoryItems.FirstOrDefault(
                x => x.ProductId == ProductId);

        AvailableQuantity =
            inventory?.AvailableQuantity ?? 0;
    }
}