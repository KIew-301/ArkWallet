using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionActivationServices;

internal class SubscriptionActivationService(
    ArkWalletDbContext dbContext,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<SubscriptionActivationService> logger) : ISubscriptionActivationService
{
    public async Task<SubscriptionActivationResult> ActivateAsync(long traderTelegramId, int subscriptionId, SubscriptionPeriod period, decimal amountRubles, string? transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sub = await dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
            if (sub is null)
                return new SubscriptionActivationResult(false, null);

            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderTelegramId, cancellationToken);
            if (trader is null)
                return new SubscriptionActivationResult(false, null);

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var currentActive = await GetActiveSubscriptionAsync(trader, now, cancellationToken);

            var expiry = ResolveExpiry(sub, currentActive, trader, period, now);

            trader.SubscriptionId = sub.Id;
            trader.SubscriptionExpiresAtUtc = expiry;

            var historyEntry = new SubscriptionPurchaseHistory
            {
                TraderId = trader.TelegramId,
                SubscriptionId = sub.Id,
                PriceRubles = amountRubles,
                PurchasedAtUtc = now,
                ExpiresAtUtc = expiry,
                TransactionId = transactionId
            };

            dbContext.SubscriptionPurchaseHistory.Add(historyEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            var operation = ResolveOperation(currentActive, sub);

            await eventPublisher.PublishAsync(
                new TraderSubscriptionChangedEvent(
                    trader.TelegramId,
                    operation,
                    sub.Name,
                    sub.Level,
                    expiry),
                cancellationToken);
            return new SubscriptionActivationResult(true, expiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при активации подписки для трейдера {TraderId}, подписка {SubscriptionId}", traderTelegramId, subscriptionId);
            return new SubscriptionActivationResult(false, null);
        }
    }

    private static SubscriptionChangeOperation ResolveOperation(Subscription? currentActive, Subscription sub)
    {
        if (currentActive is null)
            return SubscriptionChangeOperation.Purchased;

        return sub.Level > currentActive.Level
            ? SubscriptionChangeOperation.Upgraded
            : SubscriptionChangeOperation.Renewed;
    }

    private async Task<Subscription?> GetActiveSubscriptionAsync(Trader trader, DateTime now, CancellationToken cancellationToken)
    {
        if (!trader.SubscriptionId.HasValue) return null;
        var current = await dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == trader.SubscriptionId.Value, cancellationToken);
        var isActive = current is not null
            && (trader.SubscriptionExpiresAtUtc is null || trader.SubscriptionExpiresAtUtc.Value > now);
        return isActive ? current : null;
    }

    private static DateTime? ResolveExpiry(Subscription target, Subscription? currentActive, Trader trader, SubscriptionPeriod period, DateTime now)
    {
        if (target.DurationMinutes is null)
            return null;

        var duration = period.GetDurationMinutes();

        if (currentActive is not null && target.Level == currentActive.Level && trader.SubscriptionExpiresAtUtc.HasValue)
            return trader.SubscriptionExpiresAtUtc.Value.AddMinutes(duration);

        if (currentActive is not null && target.Level > currentActive.Level)
            return now.AddMinutes(duration + ComputeUpgradeBonusMinutes(trader, currentActive, target, now));

        return now.AddMinutes(duration);
    }

    private static int ComputeUpgradeBonusMinutes(Trader trader, Subscription currentActive, Subscription target, DateTime now)
    {
        if (!trader.SubscriptionExpiresAtUtc.HasValue || target.PriceMonthRubles <= 0)
            return 0;

        var remaining = trader.SubscriptionExpiresAtUtc.Value - now;
        if (remaining <= TimeSpan.Zero)
            return 0;

        var ratio = currentActive.PriceMonthRubles / target.PriceMonthRubles;
        return (int)Math.Floor((decimal)remaining.TotalMinutes * ratio);
    }
}
