using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Entities;
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
            var validationError = ValidateOrderBookParameters(symbol, buyOrdersCount, sellOrdersCount);
            if (validationError != null)
                return Fail(validationError);

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

            var bids = BuildOrderBookEntries(allOrders, OrderType.Buy, buyOrdersCount);
            var asks = BuildOrderBookEntries(allOrders, OrderType.Sell, sellOrdersCount);

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

    private static string? ValidateOrderBookParameters(string symbol, int buyOrdersCount, int sellOrdersCount)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "Символ токена не может быть пустым";

        if (buyOrdersCount <= 0)
            return "Количество ордеров на покупку должно быть больше 0";

        if (sellOrdersCount <= 0)
            return "Количество ордеров на продажу должно быть больше 0";

        return null;
    }

    private static List<OrderBookEntry> BuildOrderBookEntries(
        List<TradeOrder> orders, OrderType type, int count)
    {
        var direction = type == OrderType.Buy ? "Buy" : "Sell";
        var query = type == OrderType.Buy
            ? orders.Where(o => o.Type == OrderType.Buy).OrderByDescending(o => o.Price)
            : orders.Where(o => o.Type == OrderType.Sell).OrderBy(o => o.Price);

        return query
            .Take(count)
            .Select(o => new OrderBookEntry(direction, o.Price, o.GetRemainingQuantity(), o.Price * o.GetRemainingQuantity()))
            .ToList();
    }
}
