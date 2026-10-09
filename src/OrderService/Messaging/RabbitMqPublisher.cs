
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
        string hostName =
            _configuration["RabbitMQ:HostName"] ?? "localhost";

        string userName =
            _configuration["RabbitMQ:UserName"] ?? "guest";

        string password =
            _configuration["RabbitMQ:Password"] ?? "guest";

        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };

        await using IConnection connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

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

        string json = JsonSerializer.Serialize(orderPlaced);
        byte[] body = Encoding.UTF8.GetBytes(json);

        BasicProperties properties = new BasicProperties
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
