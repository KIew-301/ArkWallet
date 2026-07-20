using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TradeOrderServices;

using static Result<OrderBookResult>;

internal class OrderBookService(
    ArkWalletDbContext dbContext,
    ILogger<OrderBookService> logger) : IOrderBookService
{
    public async Task<Result<OrderBookResult>> GetOrderBookAsync(
        string symbol, int buyOrdersCount, int sellOrdersCount)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Символ токена не может быть пустым");

            if (buyOrdersCount <= 0)
                return Fail("Количество ордеров на покупку должно быть больше 0");

            if (sellOrdersCount <= 0)
                return Fail("Количество ордеров на продажу должно быть больше 0");

            symbol = symbol.ToUpper();

            var tokenExists = await dbContext.CharacterTokens
                .AsNoTracking()
                .AnyAsync(t => t.Symbol == symbol);

            if (!tokenExists)
                return Fail("Токена не существует");

            var allOrders = await dbContext.TradeOrders
                .AsNoTracking()
                .Where(o => o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .ToListAsync();

            var bids = allOrders
                .Where(o => o.Type == OrderType.Buy)
                .OrderByDescending(o => o.Price)
                .Take(buyOrdersCount)
                .Select(o => new OrderBookEntry("Buy", o.Price, o.GetRemainingQuantity(), o.Price * o.GetRemainingQuantity()))
                .ToList();

            var asks = allOrders
                .Where(o => o.Type == OrderType.Sell)
                .OrderBy(o => o.Price)
                .Take(sellOrdersCount)
                .Select(o => new OrderBookEntry("Sell", o.Price, o.GetRemainingQuantity(), o.Price * o.GetRemainingQuantity()))
                .ToList();

            var bestBid = bids.Count > 0 ? bids[0].Price : 0m;
            var bestAsk = asks.Count > 0 ? asks[0].Price : 0m;
            var spread = bestAsk > 0 && bestBid > 0 ? bestAsk - bestBid : 0m;

            return Ok(new OrderBookResult(
                symbol,
                bestBid,
                bestAsk,
                spread,
                bids,
                asks
            ));
        }, logger, nameof(OrderBookService));
    }
}
