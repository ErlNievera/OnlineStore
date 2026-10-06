using System.Net;
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
}
