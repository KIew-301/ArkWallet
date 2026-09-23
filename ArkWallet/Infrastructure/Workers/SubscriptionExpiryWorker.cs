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
        await RunStartupFallbackAsync(stoppingToken);

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

                await WaitForNextExpiryAndDowngradeAsync(service, delayCts.Token, stoppingToken);
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

    private async Task WaitForNextExpiryAndDowngradeAsync(
        ISubscriptionExpiryService service,
        CancellationToken recomputeToken,
        CancellationToken stoppingToken)
    {
        var next = await service.GetNextExpiryAsync(stoppingToken);

        if (next is null)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            return;
        }

        var delay = CalculateDelay(next);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, recomputeToken);

        try
        {
            await Task.Delay(delay, linked.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var downgradedNow = await service.DowngradeToBasicAsync(next.TraderId, stoppingToken);
        if (downgradedNow > 0)
            logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", downgradedNow);
    }

    private async Task RunStartupFallbackAsync(CancellationToken stoppingToken)
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
    }

    private static TimeSpan CalculateDelay(TraderSubscriptionExpiry next)
    {
        var delay = next.ExpiresAtUtc - DateTime.UtcNow;
        if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
        if (delay > TimeSpan.FromHours(1)) delay = TimeSpan.FromHours(1);
        return delay;
    }
}
