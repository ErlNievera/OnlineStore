using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class OrderConfirmationModel : PageModel
{
    private readonly OrderApiClient _orderApiClient;

    public OrderConfirmationModel(OrderApiClient orderApiClient)
    {
        _orderApiClient = orderApiClient;
    }

    public OrderResultDto? Order { get; private set; }

    public async Task<IActionResult>
    OnGetAsync(Guid orderId)
    {
        try
        {
            Order = await _orderApiClient.GetOrderAsync(orderId);

            if (Order is null)
            {
                return NotFound();
            }

            return Page();
        }
        catch
        {
            return NotFound();
        }
    }
}
