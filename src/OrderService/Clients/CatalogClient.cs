using System.Net;
using System.Net.Http.Json;

namespace OrderService.Clients;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogProductResponse?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"catalog/v1/products/{productId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CatalogProductResponse>(
            cancellationToken: cancellationToken);
    }
}
