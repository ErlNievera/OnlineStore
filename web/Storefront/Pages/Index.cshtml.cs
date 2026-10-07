using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class IndexModel : PageModel
{
    private readonly CatalogApiClient _catalogApiClient;
    private readonly InventoryApiClient _inventoryApiClient;

    public IndexModel(
        CatalogApiClient catalogApiClient,
        InventoryApiClient inventoryApiClient)
    {
        _catalogApiClient = catalogApiClient;
        _inventoryApiClient = inventoryApiClient;
    }

    public List<ProductDto> Products { get; private set; } = new();

    public List<InventoryItemDto> InventoryItems { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Products = await _catalogApiClient.GetProductsAsync();

        InventoryItems = await _inventoryApiClient.GetItemsAsync();
    }
}