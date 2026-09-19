using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseServices;

internal class SubscriptionPurchaseService(
    ArkWalletDbContext dbContext,
    IPaymentIntegrationService payment,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<SubscriptionPurchaseService> logger) : IPurchaseService
{
    public async Task<PurchaseResult> PurchaseAsync(long traderTelegramId, int subscriptionId, SubscriptionPeriod period, CancellationToken cancellationToken = default)
    {
        try
        {
            var sub = await dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
            if (sub is null)
                return PurchaseResult.Fail("Подписка не найдена");

            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderTelegramId, cancellationToken);
            if (trader is null)
                return PurchaseResult.Fail("Трейдер не найден");

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var currentActive = await GetActiveSubscriptionAsync(trader, now, cancellationToken);
            // Запрет покупки уровня ниже активной — до оплаты, чтобы не списывать деньги.
            if (currentActive is not null && sub.Level < currentActive.Level)
                return PurchaseResult.Fail("Нельзя приобрести подписку уровня ниже активной");

            decimal amount = GetPrice(sub, period);

            var paymentResult = await payment.CreatePaymentAsync(
                new PaymentRequest
                {
                    TraderTelegramId = traderTelegramId,
                    AmountRubles = amount,
                    SubscriptionId = sub.Id,
                    Description = $"Покупка подписки {sub.Name} на {period.ToDisplayName()}"
                },
                cancellationToken);

            if (!paymentResult.IsSuccess)
                return PurchaseResult.Fail("Платеж не прошел");

            var expiry = ResolveExpiry(sub, currentActive, trader, period, now);

            trader.SubscriptionId = sub.Id;
            trader.SubscriptionExpiresAtUtc = expiry;

            var historyEntry = new SubscriptionPurchaseHistory
            {
                TraderId = traderTelegramId,
                SubscriptionId = sub.Id,
                PriceRubles = amount,
                PurchasedAtUtc = now,
                ExpiresAtUtc = expiry,
                TransactionId = paymentResult.TransactionId
            };

            dbContext.SubscriptionPurchaseHistory.Add(historyEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            await eventPublisher.PublishAsync(new TraderSubscriptionChangedEvent(traderTelegramId), cancellationToken);

            return new PurchaseResult(true, $"Подписка приобретена на {period.ToDisplayName()}", paymentResult.TransactionId, expiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при покупке подписки для трейдера {TraderId}, подписка {SubscriptionId}", traderTelegramId, subscriptionId);
            return PurchaseResult.Fail($"Произошла ошибка: {ex.Message}");
        }
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

    private static decimal GetPrice(Subscription sub, SubscriptionPeriod period) => period switch
    {
        SubscriptionPeriod.Week => sub.PriceWeekRubles,
        SubscriptionPeriod.Month => sub.PriceMonthRubles,
        SubscriptionPeriod.Year => sub.PriceYearRubles,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
    };

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
