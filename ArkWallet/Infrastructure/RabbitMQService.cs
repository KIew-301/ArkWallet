using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ArkWallet.Infrastructure
{
    internal class RabbitMQService : IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        public IChannel GetChannel() => _channel;

        public RabbitMQService(IConfiguration configuration)
        {
            var host = configuration["RabbitMQ:HostName"] ?? "localhost";
            var user = configuration["RabbitMQ:UserName"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";

            var factory = new ConnectionFactory()
            {
                HostName = host,
                UserName = user,
                Password = password
            };

            // ✅ Используем async методы
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            CreateQueues();
        }

        public void CreateQueues()
        {
            try
            {
                _channel.QueueDeclareAsync(
                    queue: "notification",
                    durable: true,
                    exclusive: false,
                    autoDelete: false
                ).GetAwaiter().GetResult();

                Console.WriteLine("Queue 'notification' created or already exists");
            }
            catch (OperationInterruptedException ex)
            {
                Console.WriteLine($"Queue exists with different parameters: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
                await _channel.CloseAsync();

            if (_connection != null)
                await _connection.CloseAsync();

            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
