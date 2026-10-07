using Storefront.Models;

namespace Storefront.Services;

public class CatalogApiClient
{
    private readonly HttpClient _httpClient;

    public CatalogApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var response = await _httpClient.GetAsync("/catalog/v1/products");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ProductDto>>() ?? new List<ProductDto>();
    }
}