using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices;

public interface IOrderBookService
{
    Task<Result<OrderBookResult>> GetOrderBookAsync(string symbol, int buyOrdersCount, int sellOrdersCount);
}

public record OrderBookResult(
    string Symbol,
    decimal BestBid,
    decimal BestAsk,
    decimal Spread,
    List<OrderBookEntry> Bids,
    List<OrderBookEntry> Asks
);

public record OrderBookEntry(
    string Side,
    decimal Price,
    int Quantity,
    decimal TotalCost
);
