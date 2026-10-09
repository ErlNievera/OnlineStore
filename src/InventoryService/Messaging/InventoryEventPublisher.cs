using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Events;

namespace InventoryService.Messaging;

public class InventoryEventPublisher
{
    private readonly IConfiguration _configuration;

    public InventoryEventPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishInventoryReservedAsync(
        InventoryReserved inventoryReserved,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(
            inventoryReserved,
            "inventory.reserved",
            cancellationToken);
    }

    public async Task PublishInventoryReservationFailedAsync(
        InventoryReservationFailed inventoryReservationFailed,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(
            inventoryReservationFailed,
            "inventory.reservation.failed",
            cancellationToken);
    }

    private async Task PublishAsync<T>(
        T message,
        string routingKey,
        CancellationToken cancellationToken)
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
            exchange: "inventory",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        string json = JsonSerializer.Serialize(message);
        byte[] body = Encoding.UTF8.GetBytes(json);

        BasicProperties properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = typeof(T).Name
        };

        await channel.BasicPublishAsync(
            exchange: "inventory",
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}