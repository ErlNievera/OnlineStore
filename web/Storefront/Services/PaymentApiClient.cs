using System.Net.Http.Json;
using Storefront.Models;

namespace Storefront.Services;

public class PaymentApiClient
{
    private readonly HttpClient _httpClient;

    public PaymentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentResultDto> CreatePaymentAsync(
        CreatePaymentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/payments/v1/payments",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Payment failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<PaymentResultDto>()
            ?? throw new InvalidOperationException("Payment response was empty.");
    }
}