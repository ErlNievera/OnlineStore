using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Events;

namespace OrderService.Messaging;

public class RabbitMqPublisher
{
    private readonly IConfiguration _configuration;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishOrderPlacedAsync(
        OrderPlaced orderPlaced,
        CancellationToken cancellationToken = default)
    {
        var hostName =
            _configuration["RabbitMQ:HostName"] ?? "localhost";

        var userName =
            _configuration["RabbitMQ:UserName"] ?? "guest";

        var password =
            _configuration["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: "orders",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: "inventory-order-placed",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: "inventory-order-placed",
            exchange: "orders",
            routingKey: "order.placed",
            cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(orderPlaced);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = nameof(OrderPlaced)
        };

        await channel.BasicPublishAsync(
            exchange: "orders",
            routingKey: "order.placed",
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
