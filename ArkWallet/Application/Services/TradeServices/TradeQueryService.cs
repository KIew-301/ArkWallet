using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TradeServices;

using static Result<List<TradeInfo>>;

internal class TradeQueryService(
    ArkWalletDbContext dbContext,
    ILogger<TradeQueryService> logger) : ITradeQueryService
{
    public async Task<Result<List<TradeInfo>>> GetTraderTradesAsync(long traderTelegramId, bool withTokenInfo = false)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var trades = await dbContext.Trades
                .Where(t => t.BuyerId == traderTelegramId || t.SellerId == traderTelegramId)
                .Include(t => t.CharacterToken)
                .OrderByDescending(t => t.ExecutedAt)
                .ToListAsync();

            if (!trades.Any())
                return Ok(new List<TradeInfo>());

            var result = trades
                .Where(t => t.CharacterToken != null)
                .Select(t =>
                {
                    var isBuyer = t.BuyerId == traderTelegramId;
                    var info = TradeInfo.FromEntity(t);

                    return info with
                    {
                        TraderRole = isBuyer ? "Buyer" : "Seller",
                        Profit = isBuyer ? -(t.Quantity * t.Price) : (t.Quantity * t.Price)
                    };
                })
                .ToList();

            return Ok(result);
        }, logger, nameof(TradeQueryService));
    }
}