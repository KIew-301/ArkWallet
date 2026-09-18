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

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            DateTime? expiry = sub.DurationMinutes.HasValue
                ? now.AddMinutes(sub.DurationMinutes.Value)
                : (DateTime?)null;

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
}
