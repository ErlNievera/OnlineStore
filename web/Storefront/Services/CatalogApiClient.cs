using System.Net;
using System.Net.Http.Json;
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
        HttpResponseMessage response =
            await _httpClient.GetAsync("/catalog/v1/products");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ProductDto>>()
            ?? new List<ProductDto>();
    }

    public async Task<ProductDto?> GetProductAsync(Guid id)
    {
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/catalog/v1/products/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }
}