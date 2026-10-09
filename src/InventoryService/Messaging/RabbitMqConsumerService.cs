
using System.Text;
using InventoryService.Consumers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Messaging;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RabbitMqConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
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

        _connection = await factory.CreateConnectionAsync(
            stoppingToken);

        _channel = await _connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: "orders",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // This queue declaration matches the existing queue.
        // DLQ configuration will be handled separately.
        await _channel.QueueDeclareAsync(
            queue: "inventory-order-placed",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: "inventory-order-placed",
            exchange: "orders",
            routingKey: "order.placed",
            cancellationToken: stoppingToken);

        // Process one unacknowledged message at a time.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer =
            new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            string message = Encoding.UTF8.GetString(
                eventArgs.Body.ToArray());

            try
            {
                using IServiceScope scope =
                    _scopeFactory.CreateScope();

                OrderPlacedConsumer handler =
                    scope.ServiceProvider
                        .GetRequiredService<OrderPlacedConsumer>();

                await handler.HandleAsync(
                    message,
                    stoppingToken);

                await _channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "RabbitMQ message processed and acknowledged.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process RabbitMQ message. Rejecting without requeue.");

                await _channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "inventory-order-placed",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "RabbitMQ consumer is listening to inventory-order-placed.");

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
