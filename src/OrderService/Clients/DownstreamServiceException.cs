namespace OrderService.Clients;

public class DownstreamServiceException : Exception
{
    public string ServiceName { get; }

    public int StatusCode { get; }

    public DownstreamServiceException(
        string serviceName,
        int statusCode)
        : base($"{serviceName} returned an error with status code {statusCode}.")
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
    }
}