using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseServices;

internal class SubscriptionPurchaseService(
    ArkWalletDbContext dbContext,
    IPaymentIntegrationService payment,
    ISubscriptionActivationService activationService,
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
                    Description = $"Покупка подписки {sub.Name} на {period.ToDisplayName()}",
                    Period = (int)period,
                    SavePaymentMethod = true,
                },
                cancellationToken);

            if (!paymentResult.IsSuccess)
                return PurchaseResult.Fail("Платеж не прошел");

            if (!string.IsNullOrEmpty(paymentResult.SavedPaymentMethodId))
            {
                trader.SavedPaymentMethodId = paymentResult.SavedPaymentMethodId;
            }

            if (paymentResult.RequiresConfirmation)
            {
                return await SavePendingPaymentAsync(trader, sub, period, amount, paymentResult, now, cancellationToken);
            }

            var activationResult = await activationService.ActivateAsync(traderTelegramId, sub.Id, period, amount, paymentResult.TransactionId, cancellationToken);
            if (!activationResult.Success)
                return PurchaseResult.Fail("Не удалось активировать подписку");

            return new PurchaseResult(true, $"Подписка приобретена на {period.ToDisplayName()}", paymentResult.TransactionId, activationResult.Expiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при покупке подписки для трейдера {TraderId}, подписка {SubscriptionId}", traderTelegramId, subscriptionId);
            return PurchaseResult.Fail($"Произошла ошибка: {ex.Message}");
        }
    }

    private async Task<PurchaseResult> SavePendingPaymentAsync(
        Trader trader,
        Subscription sub,
        SubscriptionPeriod period,
        decimal amount,
        PaymentResult paymentResult,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = trader.TelegramId,
            SubscriptionId = sub.Id,
            Period = (int)period,
            AmountRubles = amount,
            ExternalPaymentId = paymentResult.PaymentId ?? paymentResult.TransactionId ?? string.Empty,
            Status = "pending",
            ConfirmationUrl = paymentResult.ConfirmationUrl,
            PaymentMethodId = paymentResult.SavedPaymentMethodId,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PurchaseResult(true, $"Счёт на оплату создан для подписки {sub.Name}", paymentResult.TransactionId, null)
        {
            ConfirmationUrl = paymentResult.ConfirmationUrl,
            RequiresConfirmation = true
        };
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

    private static decimal GetPrice(Subscription sub, SubscriptionPeriod period) => period switch
    {
        SubscriptionPeriod.Week => sub.PriceWeekRubles,
        SubscriptionPeriod.Month => sub.PriceMonthRubles,
        SubscriptionPeriod.Year => sub.PriceYearRubles,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
    };
}


