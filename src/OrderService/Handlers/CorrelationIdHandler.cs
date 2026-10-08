using Microsoft.AspNetCore.Http;

namespace OrderService.Handlers;

public class CorrelationIdHandler : DelegatingHandler
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? correlationId =
            _httpContextAccessor.HttpContext?
                .Request.Headers[CorrelationIdHeader]
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        request.Headers.Remove(CorrelationIdHeader);
        request.Headers.Add(CorrelationIdHeader, correlationId);

        HttpResponseMessage response = await base.SendAsync(
            request,
            cancellationToken);

        if (!response.Headers.Contains(CorrelationIdHeader))
        {
            response.Headers.Add(CorrelationIdHeader, correlationId);
        }

        return response;
    }
}