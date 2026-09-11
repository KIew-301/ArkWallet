using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using Records = global::ArkWallet.Infrastructure.Data;
using TradingOrderType = ArkWallet.Core.TradingContext.Domain.TraderAggregate.OrderType;
using TradingOrderStatus = ArkWallet.Core.TradingContext.Domain.TraderAggregate.OrderStatus;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;

/// <summary>
/// Транспорт данных между записями БД и движком сетки маркет-мейкера.
/// </summary>
internal static class MarketMakerGridMapper
{
    internal static global::ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMaker ToMarketMaker(
        Records.MarketMakerBot source)
        => global::ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMaker.Load(
        source.Id,
        source.TraderId,
        source.Symbol,
        (MarketMakerRole)(int)source.Role,
        source.BasePower,
        source.IsActive,
        source.CreatedAt);

    internal static List<CreateOrderCommand> CollectGridCommands(
        MarketMakerGridEngine engine,
        Records.MarketMakerBot bot,
        decimal currentPrice,
        List<Records.TradeOrder> existingOrders)
    {
        var domainBot = ToMarketMaker(bot);
        var domainOrders = existingOrders.Select(ToOrder).ToList();

        var commands = engine.GetOrdersToPlace(domainBot, currentPrice, domainOrders);

        return commands
            .Select(c => new CreateOrderCommand(c.TraderId, c.Direction, c.Symbol, c.Quantity, c.Price))
            .ToList();
    }

    private static Order ToOrder(Records.TradeOrder source) => Order.Load(
        source.Id,
        (TradingOrderType)(int)source.Type,
        (TradingOrderStatus)(int)source.Status,
        source.CharacterTokenId,
        source.Price,
        source.AverageExecutePrice,
        source.Quantity,
        source.FilledQuantity,
        source.CreatedAt,
        source.ExecutedAt);
}