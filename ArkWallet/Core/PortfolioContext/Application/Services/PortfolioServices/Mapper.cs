using ArkWallet.Core.PortfolioContext.Domain.Position;
using Records = global::ArkWallet.Infrastructure.Data;

namespace ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices;

/// <summary>
/// Maps between PortfolioItem persistence records and the Position aggregate.
/// </summary>
internal static class Mapper
{
    internal static Position ToPosition(Records.PortfolioItem item)
    {
        return Position.Load(new PositionData(
            item.Id,
            item.TraderId,
            item.CharacterTokenId,
            item.Quantity,
            item.SellingQuantity,
            item.ReserveQuantity,
            item.AverageBuyPrice,
            item.AverageSellPrice,
            item.AverageReservePrice,
            item.AcquiredAt));
    }

    internal static void ApplyToRecord(Records.PortfolioItem record, Position position)
    {
        record.Update(
            position.Quantity,
            position.SellingQuantity,
            position.ReserveQuantity,
            position.AverageBuyPrice,
            position.AverageSellPrice,
            position.AverageReservePrice);
    }

    internal static Records.PortfolioItem ToRecord(Position position)
    {
        return Records.PortfolioItem.Create(position.TraderId, position.Symbol, position.Quantity, position.AverageBuyPrice);
    }
}
