using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;

public interface ISubscriptionActivationService
{
    Task<SubscriptionActivationResult> ActivateAsync(long traderTelegramId, int subscriptionId, SubscriptionPeriod period, decimal amountRubles, string? transactionId, CancellationToken cancellationToken = default);
}

public sealed record SubscriptionActivationResult(bool Success, DateTime? Expiry);
