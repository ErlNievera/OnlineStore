using System.Net.Http.Json;

namespace OrderService.Clients;

public class InventoryClient
{
    private readonly HttpClient _httpClient;

    public InventoryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<InventoryItemResponse?> GetInventoryByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            "inventory/v1/items",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var items = await response.Content
            .ReadFromJsonAsync<List<InventoryItemResponse>>(
                cancellationToken: cancellationToken);

        return items?
            .FirstOrDefault(x => x.ProductId == productId);
    }

    public async Task<bool> ReserveStockAsync(
        Guid inventoryItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Quantity = quantity
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"inventory/v1/items/{inventoryItemId}/reserve",
            request,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReleaseStockAsync(
        Guid inventoryItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Quantity = quantity
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"inventory/v1/items/{inventoryItemId}/release",
            request,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}