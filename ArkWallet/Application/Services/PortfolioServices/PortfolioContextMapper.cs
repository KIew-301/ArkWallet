using ArkWallet.Domain.PortfolioContext;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.PortfolioServices;

/// <summary>
/// Maps between PortfolioItem persistence records and the Position aggregate.
/// </summary>
internal static class PortfolioContextMapper
{
    internal static Position ToPosition(Records.PortfolioItem item)
    {
        return Position.Load(
            item.Id,
            item.TraderTelegramId,
            item.CharacterTokenId,
            item.Quantity,
            item.SellingQuantity,
            item.ReserveQuantity,
            item.AverageBuyPrice,
            item.AverageSellPrice,
            item.AverageReservePrice,
            item.AcquiredAt);
    }

    internal static void ApplyToRecord(Records.PortfolioItem record, Position position)
    {
        record.ApplyState(
            position.Quantity,
            position.SellingQuantity,
            position.ReserveQuantity,
            position.AverageBuyPrice,
            position.AverageSellPrice,
            position.AverageReservePrice);
    }

    internal static Records.PortfolioItem ToRecord(Position position)
    {
        return Records.PortfolioItem.Create(position.TraderTelegramId, position.Symbol, position.Quantity, position.AverageBuyPrice);
    }
}
