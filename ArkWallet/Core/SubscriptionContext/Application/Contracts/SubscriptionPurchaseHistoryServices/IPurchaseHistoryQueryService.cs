using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseHistoryServices;

public interface IPurchaseHistoryQueryService
{
    Task<Result<List<PurchaseHistoryEntry>>> GetHistoryAsync(long traderTelegramId);
}
