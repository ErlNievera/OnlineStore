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
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"catalog/v1/products/{productId}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new DownstreamServiceException(
                    "Catalog Service",
                    (int)response.StatusCode);
            }

            CatalogProductResponse? product =
                await response.Content.ReadFromJsonAsync<CatalogProductResponse>(
                    cancellationToken: cancellationToken);

            return product;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Catalog Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Catalog Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }
}