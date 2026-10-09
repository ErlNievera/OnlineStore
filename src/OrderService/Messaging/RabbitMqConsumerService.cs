using System.Text;
using OrderService.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Messaging;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using IConnection connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using IChannel channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: "inventory",
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: "order-inventory-results",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: "order-inventory-results",
                    exchange: "inventory",
                    routingKey: "inventory.reserved",
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: "order-inventory-results",
                    exchange: "inventory",
                    routingKey: "inventory.reservation.failed",
                    cancellationToken: stoppingToken);

                AsyncEventingBasicConsumer consumer =
                    new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (_, eventArgs) =>
                {
                    string message = Encoding.UTF8.GetString(
                        eventArgs.Body.ToArray());

                    string? routingKey = eventArgs.RoutingKey;

                    try
                    {
                        using IServiceScope scope =
                            _scopeFactory.CreateScope();

                        InventoryResultConsumer handler =
                            scope.ServiceProvider.GetRequiredService<
                                InventoryResultConsumer>();

                        if (routingKey == "inventory.reserved")
                        {
                            await handler.HandleReservedAsync(
                                message,
                                stoppingToken);
                        }
                        else if (routingKey ==
                                 "inventory.reservation.failed")
                        {
                            await handler.HandleReservationFailedAsync(
                                message,
                                stoppingToken);
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Unsupported routing key: {routingKey}");
                        }

                        await channel.BasicAckAsync(
                            eventArgs.DeliveryTag,
                            multiple: false,
                            cancellationToken: stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to process inventory result with routing key {RoutingKey}.",
                            routingKey);

                        await channel.BasicNackAsync(
                            eventArgs.DeliveryTag,
                            multiple: false,
                            requeue: false,
                            cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken);

                await channel.BasicConsumeAsync(
                    queue: "order-inventory-results",
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "OrderService is listening for inventory result events.");

                await Task.Delay(
                    Timeout.Infinite,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "RabbitMQ consumer connection failed. Retrying in 5 seconds.");

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
