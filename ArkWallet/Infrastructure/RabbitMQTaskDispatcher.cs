using ArkWallet.Application.Contracts.Other;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ArkWallet.Infrastructure
{
    [ExcludeFromCodeCoverage(Justification = "Инфраструктурный диспетчер задач RabbitMQ, зависит от внешнего брокера. Тестируется интеграционно.")]
    internal class RabbitMQTaskDispatcher : ITaskDispatcher
    {
        private readonly RabbitMQService _rabbitMQService;

        public RabbitMQTaskDispatcher(RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        public async Task SendTaskAsync(string taskType, object taskData)
        {
            try
            {
                var json = JsonConvert.SerializeObject(taskData);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    MessageId = Guid.NewGuid().ToString(),
                    Type = taskType
                };

                await _rabbitMQService.GetChannel().BasicPublishAsync("", taskType, false, properties, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
