using ArkWallet.Domain.TradingContext;
using Records = global::ArkWallet.Domain.Entities;
using ValueObjects = global::ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Services.TradeOrderServices;

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
        item.ApplyState(
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

    internal static void ApplyTo(Records.PortfolioItem target, PortfolioItem source) => target.ApplyState(
        source.Quantity,
        source.SellingQuantity,
        source.ReserveQuantity,
        source.AverageBuyPrice,
        source.AverageSellPrice,
        source.AverageReservePrice);

    internal static void ApplyTo(Records.Trader target, Trader source)
        => target.AddToBalance(source.Balance - target.Balance);

    internal static void ApplyTo(Records.CharacterToken target, Token source)
        => target.UpdatePrice(source.CurrentPrice);
}
