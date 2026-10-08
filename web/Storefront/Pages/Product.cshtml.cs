using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class ProductModel : PageModel
{
    private readonly CatalogApiClient _catalogApiClient;
    private readonly InventoryApiClient _inventoryApiClient;

    public ProductModel(
        CatalogApiClient catalogApiClient,
        InventoryApiClient inventoryApiClient)
    {
        _catalogApiClient = catalogApiClient;
        _inventoryApiClient = inventoryApiClient;
    }

    public ProductDto? Product { get; private set; }

    public InventoryItemDto? Inventory { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Product = await _catalogApiClient.GetProductAsync(id);

        if (Product is null)
        {
            return NotFound();
        }

        List<InventoryItemDto> inventoryItems =
            await _inventoryApiClient.GetItemsAsync();

        Inventory = inventoryItems
            .FirstOrDefault(x => x.ProductId == Product.Id);

        return Page();
    }
}