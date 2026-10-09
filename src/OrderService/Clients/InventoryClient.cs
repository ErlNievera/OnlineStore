using System.Net;
using System.Net.Http.Json;
using OrderService.Clients.Generated;

namespace OrderService.Clients;

public class InventoryClient
{
    private readonly HttpClient _httpClient;
    private readonly IInventoryApiClient _inventoryApiClient;

    public InventoryClient(
        HttpClient httpClient,
        IInventoryApiClient inventoryApiClient)
    {
        _httpClient = httpClient;
        _inventoryApiClient = inventoryApiClient;
    }

    public async Task<InventoryItemResponse?> GetInventoryByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ICollection<OrderService.Clients.Generated.InventoryItemResponse> items =
                await _inventoryApiClient.GetInventoryItemsAsync(
                    cancellationToken);

            OrderService.Clients.Generated.InventoryItemResponse? item =
                items.FirstOrDefault(
                    inventoryItem => inventoryItem.ProductId == productId);

            if (item is null)
            {
                return null;
            }

            return new InventoryItemResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ReservedQuantity = item.ReservedQuantity,
                AvailableQuantity = item.AvailableQuantity,
                CreatedAt = item.CreatedAt.DateTime,
                UpdatedAt = item.UpdatedAt.DateTime
            };
        }
        catch (ApiException ex)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                ex.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
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
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"inventory/v1/items/{inventoryItemId}/reserve",
                new { Quantity = quantity },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if ((int)response.StatusCode >= 400 &&
                (int)response.StatusCode < 500)
            {
                return false;
            }

            throw new DownstreamServiceException(
                "Inventory Service",
                (int)response.StatusCode);
        }
        catch (DownstreamServiceException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
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
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"inventory/v1/items/{inventoryItemId}/release",
                new { Quantity = quantity },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if ((int)response.StatusCode >= 400 &&
                (int)response.StatusCode < 500)
            {
                return false;
            }

            throw new DownstreamServiceException(
                "Inventory Service",
                (int)response.StatusCode);
        }
        catch (DownstreamServiceException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Inventory Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }
}