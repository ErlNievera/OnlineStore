using System.Net.Http.Json;
using Storefront.Models;

namespace Storefront.Services;

public class InventoryApiClient
{
    private readonly HttpClient _httpClient;

    public InventoryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetItemsAsync()
    {
        var response = await _httpClient.GetAsync("/inventory/v1/items");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>()
               ?? new List<InventoryItemDto>();
    }
}