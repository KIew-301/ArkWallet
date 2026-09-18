using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;

/// <summary>
/// Обрабатывает истечение сроков подписок у трейдеров.
/// </summary>
internal class SubscriptionExpiryService(
    ArkWalletDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SubscriptionExpiryService> logger) : ISubscriptionExpiryService
{
    /// <summary>
    /// Переводит истёкших трейдеров на базовую (бессрочную) подписку.
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

        foreach (var trader in expiredTraders)
        {
            trader.SubscriptionId = basic.Id;
            trader.SubscriptionExpiresAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Переведено на базовую подписку: {Count} трейдеров", expiredTraders.Count);
        return expiredTraders.Count;
    }

    /// <summary>
    /// Возвращает время ближайшего будущего истечения подписки.
    /// </summary>
    public async Task<DateTime?> GetNextExpiryAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var hasFuture = await dbContext.Traders
            .AnyAsync(t => t.SubscriptionExpiresAtUtc != null
                        && t.SubscriptionExpiresAtUtc.Value > now, cancellationToken);

        if (!hasFuture)
        {
            return null;
        }

        var next = await dbContext.Traders
            .Where(t => t.SubscriptionExpiresAtUtc != null
                    && t.SubscriptionExpiresAtUtc.Value > now)
            .MinAsync(t => t.SubscriptionExpiresAtUtc!.Value, cancellationToken);

        return next;
    }
}
