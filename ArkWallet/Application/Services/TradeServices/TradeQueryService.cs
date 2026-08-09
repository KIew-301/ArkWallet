using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
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
                .AsNoTracking()
                .Where(t => (t.BuyerId == traderTelegramId || t.SellerId == traderTelegramId) && t.CharacterToken != null)
                .OrderByDescending(t => t.ExecutedAt)
                .Select(t => new
                {
                    t.BuyerId,
                    t.Price,
                    t.Quantity,
                    t.ExecutedAt,
                    Symbol = t.CharacterToken.Symbol,
                    Name = t.CharacterToken.Name,
                    CurrentPrice = t.CharacterToken.CurrentPrice,
                    IconUrl = t.CharacterToken.IconUrl,
                    ImageUrl = t.CharacterToken.ImageUrl
                })
                .ToListAsync();

            if (trades.Count == 0)
                return Ok(new List<TradeInfo>());

            var result = trades
                .Select(t =>
                {
                    var isBuyer = t.BuyerId == traderTelegramId;
                    return new TradeInfo(
                        isBuyer ? "Buyer" : "Seller",
                        t.Price,
                        t.Quantity,
                        isBuyer ? -(t.Quantity * t.Price) : t.Quantity * t.Price,
                        t.ExecutedAt,
                        new TokenInfo(t.Symbol, t.Name, t.CurrentPrice, t.IconUrl, t.ImageUrl));
                })
                .ToList();

            return Ok(result);
        }, logger, nameof(TradeQueryService));
    }
}