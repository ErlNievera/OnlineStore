using System.Net;
using System.Net.Http.Json;
using Storefront.Models;

namespace Storefront.Services;

public class OrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderResultDto> CreateOrderAsync(
        CreateOrderRequest request)
    {
        HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "/orders/v1/orders",
                request);

        if (!response.IsSuccessStatusCode)
        {
            string error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Order failed: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<OrderResultDto>()
            ?? throw new InvalidOperationException(
                "Order response was empty.");
    }

    public async Task<OrderResultDto?> GetOrderAsync(Guid orderId)
    {
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/orders/v1/orders/{orderId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<OrderResultDto>();
    }
}