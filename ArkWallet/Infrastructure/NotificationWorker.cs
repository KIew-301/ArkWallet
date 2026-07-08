using ArkWallet.Application.Dtos;
using ArkWallet.Telegram;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace ArkWallet.Infrastructure
{
    internal class NotificationWorker : BackgroundService
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly ILogger<NotificationWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationWorker(RabbitMQService rabbitMQService, ILogger<NotificationWorker> logger, IServiceScopeFactory scopeFactory)
        {
            _rabbitMQService = rabbitMQService;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationWorker started");

            var channel = _rabbitMQService.GetChannel();

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var bot = scope.ServiceProvider.GetRequiredService<TelegramBot>();
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var notifications = JsonConvert.DeserializeObject<List<NotificationEvent>>(message);

                    if (notifications != null)
                        foreach (var notification in notifications)
                            await bot.SendMessageToUser(notification.Id, notification.Message);

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NotificationWorker");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync("notification", autoAck: false, consumer: consumer);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
