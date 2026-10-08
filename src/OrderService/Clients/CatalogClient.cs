using System.Net;
using OrderService.Clients.Generated;

namespace OrderService.Clients;

public class CatalogClient
{
    private readonly ICatalogApiClient _catalogApiClient;

    public CatalogClient(ICatalogApiClient catalogApiClient)
    {
        _catalogApiClient = catalogApiClient;
    }

    public async Task<CatalogProductResponse?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Product product = await _catalogApiClient.GetProductByIdAsync(
                productId,
                cancellationToken);

            return new CatalogProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = (decimal)product.Price,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt.DateTime,
                UpdatedAt = product.UpdatedAt.DateTime
            };
        }
        catch (ApiException<ProblemDetails> ex)
            when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (ApiException ex)
        {
            throw new DownstreamServiceException(
                "Catalog Service",
                ex.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw new DownstreamServiceException(
                "Catalog Service",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(
                "Catalog Service",
                StatusCodes.Status504GatewayTimeout);
        }
    }
}