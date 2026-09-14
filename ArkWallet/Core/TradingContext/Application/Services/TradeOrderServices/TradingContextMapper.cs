using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Dtos;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.Events;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.General.Domain.Common;
using Records = global::ArkWallet.Infrastructure.Data;
using ValueObjects = global::ArkWallet.Core.General.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
namespace ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;

/// <summary>
/// Транспорт данных между записями БД и агрегатами контекста торгового движка.
/// </summary>
internal static class TradingContextMapper
{
    // ---- Записи БД -> агрегаты контекста ----

    internal static Token ToToken(Records.CharacterToken source) => Token.Load(
        source.Symbol,
        source.Name,
        (TokenRarity)(int)source.Rarity,
        source.CurrentPrice,
        source.TotalSupply,
        source.IsActive,
        source.ImageUrl,
        source.IconUrl,
        source.CreatedAt);

    internal static Trader ToTrader(Records.Trader source) => Trader.Load(
        source.TelegramId,
        source.Username,
        source.Balance,
        source.NotificationOn,
        source.JoinedAt);

    internal static PortfolioItem ToPortfolioItem(Records.PortfolioItem source) => PortfolioItem.Load(
        source.TraderTelegramId,
        source.Id,
        source.CharacterTokenId,
        source.Quantity,
        source.SellingQuantity,
        source.ReserveQuantity,
        source.AverageBuyPrice,
        source.AverageSellPrice,
        source.AverageReservePrice,
        source.AcquiredAt);

    internal static Order ToOrder(Records.TradeOrder source) => Order.Load(
        source.Id,
        (OrderType)(int)source.Type,
        (OrderStatus)(int)source.Status,
        source.CharacterTokenId,
        source.Price,
        source.AverageExecutePrice,
        source.Quantity,
        source.FilledQuantity,
        source.CreatedAt,
        source.ExecutedAt);

    // ---- Агрегаты контекста -> записи БД ----

    internal static Records.TradeOrder ToRecord(Order source) => new()
    {
        Id = source.Id,
        Type = (ValueObjects.OrderType)(int)source.Type,
        Status = (ValueObjects.OrderStatus)(int)source.Status,
        CharacterTokenId = source.TokenSymbol,
        TraderTelegramId = source.TraderId,
        Price = source.Price,
        AverageExecutePrice = source.AverageExecutePrice,
        Quantity = source.Quantity,
        FilledQuantity = source.FilledQuantity,
        CreatedAt = source.CreatedAt,
        ExecutedAt = source.ExecutedAt
    };

    internal static Records.Trade ToTrade(Trade source) => new()
    {
        Id = source.Id,
        BuyerId = source.BuyerId,
        SellerId = source.SellerId,
        CharacterTokenId = source.TokenSymbol,
        Price = source.Price,
        Quantity = source.Quantity,
        ExecutedAt = source.ExecutedAt
    };

    internal static Records.PortfolioItem ToPortfolio(long traderId, PortfolioItem source)
    {
        var item = Records.PortfolioItem.Create(traderId, source.TokenSymbol, source.Quantity, source.AverageBuyPrice);
        item.Update(
            source.Quantity,
            source.SellingQuantity,
            source.ReserveQuantity,
            source.AverageBuyPrice,
            source.AverageSellPrice,
            source.AverageReservePrice);
        item.Id = source.Id;
        return item;
    }

    // ---- Синхронизация записей БД <- агрегаты контекста ----

    internal static void ApplyTo(Records.TradeOrder target, Order source)
    {
        target.Type = (ValueObjects.OrderType)(int)source.Type;
        target.Status = (ValueObjects.OrderStatus)(int)source.Status;
        target.CharacterTokenId = source.TokenSymbol;
        target.TraderTelegramId = source.TraderId;
        target.Price = source.Price;
        target.AverageExecutePrice = source.AverageExecutePrice;
        target.Quantity = source.Quantity;
        target.FilledQuantity = source.FilledQuantity;
        target.CreatedAt = source.CreatedAt;
        target.ExecutedAt = source.ExecutedAt;
    }

    internal static void ApplyTo(Records.PortfolioItem target, PortfolioItem source) => target.Update(
        source.Quantity,
        source.SellingQuantity,
        source.ReserveQuantity,
        source.AverageBuyPrice,
        source.AverageSellPrice,
        source.AverageReservePrice);

    internal static void ApplyTo(Records.Trader target, Trader source)
        => target.Balance = source.Balance;

    internal static void ApplyTo(Records.CharacterToken target, Token source)
        => target.CurrentPrice = source.CurrentPrice;

    // ---- Построение контекста движка (записи БД -> агрегаты) ----

    internal static async Task<TradingEngineContext> BuildContext(
        IReadOnlyCollection<CreateOrderCommand> commands,
        bool isBuy,
        Dictionary<long, Records.Trader> oldTraders,
        Records.TradeOrder[] activeOrders,
        Dictionary<long, Records.PortfolioItem> oldPortfolios,
        Records.CharacterToken oldToken,
        IEventPublisher eventPublisher)
    {
        var context = new TradingEngineContext
        {
            Token = ToToken(oldToken),
            EventPublisher = eventPublisher,
        };
        context.Token.SetEventPublisher(eventPublisher);

        var traderIds = commands.Select(c => c.TraderId)
            .Concat(activeOrders.Select(o => o.TraderTelegramId))
            .Distinct();

        foreach (var traderId in traderIds)
        {
            if (!oldTraders.TryGetValue(traderId, out var oldTrader))
                throw new InvalidOperationException("Трейдер не найден");

            var trader = ToTrader(oldTrader);
            trader.SetEventPublisher(eventPublisher);
            context.Traders[traderId] = trader;

            if (oldPortfolios.TryGetValue(traderId, out var oldPortfolio))
                trader.AttachPortfolio(ToPortfolioItem(oldPortfolio));
        }

        foreach (var oldOrder in activeOrders)
        {
            var order = ToOrder(oldOrder);
            order.SetEventPublisher(eventPublisher);
            context.ExistingOrders.Add(order);

            if (context.Traders.TryGetValue(oldOrder.TraderTelegramId, out var owner))
                owner.AttachOrder(order);
        }

        var orderType = isBuy ? OrderType.Buy : OrderType.Sell;

        var placedOrders = new List<Order>();
        foreach (var command in commands)
        {
            var trader = context.Traders[command.TraderId];
            placedOrders.Add(await trader.PlaceOrder(orderType, command.Symbol, command.Price, command.Quantity));
        }

        context.NewOrders = isBuy
            ? placedOrders.OrderBy(o => o.Price).ToList()
            : placedOrders.OrderByDescending(o => o.Price).ToList();

        return context;
    }

    // ---- Синхронизация агрегатов -> записи БД ----

    internal static void SyncTradersAndPortfolios(TradingEngineContext context, Records.ArkWalletDbContext dbContext)
    {
        foreach (var trader in context.Traders.Values)
        {
            var trackedTrader = dbContext.Traders.Local.FirstOrDefault(t => t.TelegramId == trader.Id);
            if (trackedTrader != null)
                ApplyTo(trackedTrader, trader);

            foreach (var item in trader.Portfolio)
            {
                var trackedItem = dbContext.PortfolioItems.Local
                    .FirstOrDefault(p => p.Id == item.Id);

                if (trackedItem is null)
                    dbContext.PortfolioItems.Add(ToPortfolio(trader.Id, item));
                else
                    ApplyTo(trackedItem, item);
            }
        }
    }

    internal static void SyncToken(TradingEngineContext context, Records.ArkWalletDbContext dbContext)
    {
        var trackedToken = dbContext.CharacterTokens.Local.FirstOrDefault(t => t.Symbol == context.Token.Symbol);
        if (trackedToken != null)
            ApplyTo(trackedToken, context.Token);
    }

    // ---- Сбор записей для уведомлений ----

    internal static List<Records.TradeOrder> CollectFilledOrderRecords(
        TradingEngineContext context,
        Records.ArkWalletDbContext dbContext)
    {
        var ordersToNotify = new List<Records.TradeOrder>();

        foreach (var order in context.ExistingOrders.Where(o => o.Status == OrderStatus.Filled))
        {
            var tracked = dbContext.TradeOrders.Local.FirstOrDefault(o => o.Id == order.Id);
            if (tracked != null)
                ordersToNotify.Add(tracked);
        }

        foreach (var order in context.NewOrders.Where(o => o.IsFilled()))
        {
            var tracked = dbContext.TradeOrders.Local.FirstOrDefault(o => o.Id == order.Id);
            if (tracked != null)
                ordersToNotify.Add(tracked);
        }

        return ordersToNotify;
    }

    // ---- Конверсия результатов движка в DTO ----

    internal static OrderCreationData ToOrderCreationData(Order source)
        => new(source.IsFilled(), OrderDto.FromAggregate(source, source.TraderId));

    internal static List<OrderCreationData> ToOrderCreationResults(TradingEngineContext context)
        => context.NewOrders.Select(ToOrderCreationData).ToList();
}
