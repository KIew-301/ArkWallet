using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;

/// <summary>
/// Обрабатывает истечение сроков подписок у трейдеров.
/// </summary>
internal class SubscriptionExpiryService(
    ArkWalletDbContext dbContext,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<SubscriptionExpiryService> logger) : ISubscriptionExpiryService
{
    /// <summary>
    /// Переводит всех истёкших трейдеров на базовую (бессрочную) подписку.
    /// Фоллбэк-метод: используется при запуске воркера или после длительных простоев.
    /// Для перевода конкретного трейдера используйте DowngradeToBasicAsync(long).
    /// </summary>
    public async Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Находим базовую подписку (Level == 1), которая бессрочна (DurationMinutes null)
        var basic = await dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.Level == 1, cancellationToken);

        if (basic is null)
        {
            logger.LogInformation("Базовая подписка (Level==1) не найдена. Нечего сверять.");
            return 0;
        }

        // Находим истёкших трейдеров
        var expiredTraders = await dbContext.Traders
            .Where(t => t.SubscriptionId != null
                     && t.SubscriptionExpiresAtUtc != null
                     && t.SubscriptionExpiresAtUtc.Value <= now)
            .ToListAsync(cancellationToken);

        if (expiredTraders.Count == 0)
        {
            logger.LogDebug("Нет истёкших подписок для обработки.");
            return 0;
        }

        var affectedSubIds = expiredTraders
            .Select(t => t.SubscriptionId!.Value)
            .Distinct()
            .ToList();

        var subsById = await dbContext.Subscriptions
            .Where(s => affectedSubIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var trader in expiredTraders)
        {
            var oldSub = subsById.TryGetValue(trader.SubscriptionId!.Value, out var s) ? s : null;

            trader.SubscriptionId = basic.Id;
            trader.SubscriptionExpiresAtUtc = null;
            AddHistoryEntry(trader, basic, now);

            if (oldSub is not null)
            {
                await eventPublisher.PublishAsync(
                    new TraderSubscriptionChangedEvent(
                        trader.TelegramId,
                        SubscriptionChangeOperation.Expired,
                        oldSub.Name,
                        oldSub.Level,
                        null),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", expiredTraders.Count);
        return expiredTraders.Count;
    }

    /// <summary>
    /// Возвращает ближайшую будущую дату истечения подписки вместе с Id трейдера.
    /// </summary>
    public async Task<TraderSubscriptionExpiry?> GetNextExpiryAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var next = await dbContext.Traders
            .Where(t => t.SubscriptionExpiresAtUtc != null
                     && t.SubscriptionExpiresAtUtc.Value > now)
            .OrderBy(t => t.SubscriptionExpiresAtUtc)
            .Select(t => new TraderSubscriptionExpiry(t.TelegramId, t.SubscriptionExpiresAtUtc!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return next;
    }

    /// <summary>
    /// Переводит одного трейдера на базовую (бессрочную) подписку, если его подписка реально истекла.
    /// </summary>
    public async Task<int> DowngradeToBasicAsync(long traderId, CancellationToken cancellationToken = default)
        => await DowngradeToBasicAsync(new[] { traderId }, cancellationToken);

    /// <summary>
    /// Переводит массив трейдеров на базовую (бессрочную) подписку. Снимает дубликатов, применяет защитный фильтр по дате истечения.
    /// </summary>
    public async Task<int> DowngradeToBasicAsync(long[] traderIds, CancellationToken cancellationToken = default)
    {
        if (traderIds is null || traderIds.Length == 0) return 0;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var basic = await dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.Level == 1, cancellationToken);

        if (basic is null)
        {
            logger.LogInformation("Базовая подписка (Level==1) не найдена. Нечего переводить.");
            return 0;
        }

        var ids = traderIds.Distinct().ToList();

        // Защитный фильтр: снимаем только если ПОДПИСКА реально ИСТЕКЛА на текущий момент.
        // Это закрывает гонку, когда трейдер продлил подписку, пока воркер спал.
        var expiredTraders = await dbContext.Traders
            .Where(t => ids.Contains(t.TelegramId)
                    && t.SubscriptionId != null
                    && t.SubscriptionExpiresAtUtc != null
                    && t.SubscriptionExpiresAtUtc.Value <= now)
            .ToListAsync(cancellationToken);

        if (expiredTraders.Count == 0) return 0;

        var affectedSubIds = expiredTraders
            .Select(t => t.SubscriptionId!.Value)
            .Distinct()
            .ToList();

        var subsById = await dbContext.Subscriptions
            .Where(s => affectedSubIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var trader in expiredTraders)
        {
            var oldSub = subsById.TryGetValue(trader.SubscriptionId!.Value, out var s) ? s : null;

            trader.SubscriptionId = basic.Id;
            trader.SubscriptionExpiresAtUtc = null;
            AddHistoryEntry(trader, basic, now);

            if (oldSub is not null)
            {
                await eventPublisher.PublishAsync(
                    new TraderSubscriptionChangedEvent(
                        trader.TelegramId,
                        SubscriptionChangeOperation.Expired,
                        oldSub.Name,
                        oldSub.Level,
                        null),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", expiredTraders.Count);
        return expiredTraders.Count;
    }

    private void AddHistoryEntry(Trader trader, Subscription subscription, DateTime now)
    {
        dbContext.SubscriptionPurchaseHistory.Add(new SubscriptionPurchaseHistory
        {
            TraderId = trader.TelegramId,
            SubscriptionId = subscription.Id,
            PriceRubles = subscription.PriceRubles,
            PurchasedAtUtc = now,
            ExpiresAtUtc = null,
            TransactionId = null
        });
    }
}
