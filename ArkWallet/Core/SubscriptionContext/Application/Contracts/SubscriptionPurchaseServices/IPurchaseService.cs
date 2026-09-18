using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;

public interface IPurchaseService
{
    Task<PurchaseResult> PurchaseAsync(long traderTelegramId, int subscriptionId, CancellationToken cancellationToken = default);
}
