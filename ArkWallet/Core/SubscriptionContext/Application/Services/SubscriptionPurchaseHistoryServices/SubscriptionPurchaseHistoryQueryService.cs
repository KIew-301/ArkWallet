using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseHistoryServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseHistoryServices;

internal class SubscriptionPurchaseHistoryQueryService(ArkWalletDbContext dbContext, ILogger<SubscriptionPurchaseHistoryQueryService> logger) : IPurchaseHistoryQueryService
{
    public async Task<Result<List<PurchaseHistoryEntry>>> GetHistoryAsync(long traderTelegramId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var history = await dbContext.SubscriptionPurchaseHistory
                .Where(h => h.TraderId == traderTelegramId)
                .OrderByDescending(h => h.PurchasedAtUtc)
                .Select(h => new PurchaseHistoryEntry(
                    h.Id,
                    h.TraderId,
                    h.SubscriptionId,
                    h.Subscription.Name,
                    h.PriceRubles,
                    h.PurchasedAtUtc,
                    h.ExpiresAtUtc,
                    h.TransactionId))
                .ToListAsync();

            return Result<List<PurchaseHistoryEntry>>.Ok(history);
        }, logger, nameof(SubscriptionPurchaseHistoryQueryService));
    }
}
