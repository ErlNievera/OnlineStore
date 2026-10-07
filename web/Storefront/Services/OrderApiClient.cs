using Storefront.Models;

namespace Storefront.Services;

public class OrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderResultDto> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/orders/v1/orders", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Order failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<OrderResultDto>()
            ?? throw new InvalidOperationException("Order response was empty.");
    }
}