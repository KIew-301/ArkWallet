using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Infrastructure.Workers;

internal sealed class SubscriptionExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryWorker> logger) : BackgroundService, INotificationHandler<TraderSubscriptionChangedEvent>
{
    private int _recomputeFlag; // 1 = сигнал пересчёта

    public Task Handle(TraderSubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _recomputeFlag, 1);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ISubscriptionExpiryService>();

                var downgraded = await service.ProcessExpiredAsync(stoppingToken);
                if (downgraded > 0)
                    logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", downgraded);

                var next = await service.GetNextExpiryAsync(stoppingToken);
                TimeSpan delay = next is null
                    ? TimeSpan.FromHours(1)
                    : next.Value - DateTime.UtcNow;
                if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
                if (delay > TimeSpan.FromHours(1)) delay = TimeSpan.FromHours(1);

                Interlocked.Exchange(ref _recomputeFlag, 0);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в SubscriptionExpiryWorker");
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
    }
}
