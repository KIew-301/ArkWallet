using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Infrastructure.Workers;

internal sealed class SubscriptionExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryWorker> logger) : BackgroundService, INotificationHandler<TraderSubscriptionChangedEvent>
{
    private CancellationTokenSource? _recomputeCts;
    private readonly object _recomputeLock = new();

    public Task Handle(TraderSubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        lock (_recomputeLock)
        {
            _recomputeCts?.Cancel();
        }
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISubscriptionExpiryService>();
            var downgraded = await service.ProcessExpiredAsync(stoppingToken);
            if (downgraded > 0)
                logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", downgraded);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка фоллбэк-обработки в SubscriptionExpiryWorker");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CancellationTokenSource delayCts;
                lock (_recomputeLock)
                {
                    _recomputeCts = new CancellationTokenSource();
                    delayCts = _recomputeCts;
                }

                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ISubscriptionExpiryService>();

                var next = await service.GetNextExpiryAsync(stoppingToken);

                if (next is null)
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    continue;
                }

                TimeSpan delay = next.ExpiresAtUtc - DateTime.UtcNow;
                if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
                if (delay > TimeSpan.FromHours(1)) delay = TimeSpan.FromHours(1);

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, delayCts.Token);

                try
                {
                    await Task.Delay(delay, linked.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    continue;
                }

                var downgradedNow = await service.DowngradeToBasicAsync(next.TraderId, stoppingToken);
                if (downgradedNow > 0)
                    logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", downgradedNow);
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
