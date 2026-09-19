using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
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
    public async Task<PurchaseResult> PurchaseAsync(long traderTelegramId, int subscriptionId, CancellationToken cancellationToken = default)
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

            var paymentResult = await payment.CreatePaymentAsync(
                new PaymentRequest
                {
                    TraderTelegramId = traderTelegramId,
                    AmountRubles = sub.PriceRubles,
                    SubscriptionId = sub.Id,
                    Description = $"Покупка подписки {sub.Name}"
                },
                cancellationToken);

            if (!paymentResult.IsSuccess)
                return PurchaseResult.Fail("Платеж не прошел");

            var expiry = ResolveExpiry(sub, currentActive, trader, now);

            trader.SubscriptionId = sub.Id;
            trader.SubscriptionExpiresAtUtc = expiry;

            var historyEntry = new SubscriptionPurchaseHistory
            {
                TraderId = traderTelegramId,
                SubscriptionId = sub.Id,
                PriceRubles = sub.PriceRubles,
                PurchasedAtUtc = now,
                ExpiresAtUtc = expiry,
                TransactionId = paymentResult.TransactionId
            };

            dbContext.SubscriptionPurchaseHistory.Add(historyEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            await eventPublisher.PublishAsync(new TraderSubscriptionChangedEvent(traderTelegramId), cancellationToken);

            return new PurchaseResult(true, "Подписка приобретена", paymentResult.TransactionId, expiry);
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

    private static DateTime? ResolveExpiry(Subscription target, Subscription? currentActive, Trader trader, DateTime now)
    {
        if (currentActive is not null
            && target.Level == currentActive.Level
            && trader.SubscriptionExpiresAtUtc.HasValue
            && target.DurationMinutes.HasValue)
        {
            return trader.SubscriptionExpiresAtUtc.Value.AddMinutes(target.DurationMinutes.Value);
        }

        return target.DurationMinutes.HasValue ? now.AddMinutes(target.DurationMinutes.Value) : null;
    }
}
