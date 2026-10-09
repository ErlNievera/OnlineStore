using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderService.Clients;

public class PaymentClient
{
    private readonly HttpClient _httpClient;

    public PaymentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "payments/v1/payments",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                int statusCode = (int)response.StatusCode;

                throw new DownstreamServiceException(
                    "Payment Service",
                    statusCode >= 500
                        ? StatusCodes.Status502BadGateway
                        : statusCode);
            }

            PaymentResponse? payment =
                await response.Content.ReadFromJsonAsync<PaymentResponse>(
                    cancellationToken: cancellationToken);

            if (payment is null)
            {
                throw new DownstreamServiceException(
                    "Payment Service",
                    StatusCodes.Status502BadGateway);
            }

            return payment;
        }
        catch (DownstreamServiceException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Payment Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Payment Service",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (JsonException)
        {
            throw new DownstreamServiceException(
                "Payment Service",
                StatusCodes.Status502BadGateway);
        }
    }
}

public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "GCash";

    // Demo testing only. This does not process a real payment.
    public bool SimulateFailure { get; set; } = false;
}

public class PaymentResponse
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
