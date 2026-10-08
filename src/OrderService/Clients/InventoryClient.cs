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
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "inventory/v1/items",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new DownstreamServiceException(
                    "Inventory Service",
                    (int)response.StatusCode);
            }

            List<InventoryItemResponse>? items =
                await response.Content.ReadFromJsonAsync<List<InventoryItemResponse>>(
                    cancellationToken: cancellationToken);

            return items?.FirstOrDefault(x => x.ProductId == productId);
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }

    public async Task<bool> ReserveStockAsync(
        Guid inventoryItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                Quantity = quantity
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"inventory/v1/items/{inventoryItemId}/reserve",
                request,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }

    public async Task<bool> ReleaseStockAsync(
        Guid inventoryItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                Quantity = quantity
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"inventory/v1/items/{inventoryItemId}/release",
                request,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }
}