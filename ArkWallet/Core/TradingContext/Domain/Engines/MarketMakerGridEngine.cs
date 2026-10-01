using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;

namespace ArkWallet.Core.TradingContext.Domain.Engines;

internal class MarketMakerGridEngine
{
    private readonly Random _random = new();

    public List<CreateMarketOrderCommand> GetOrdersToPlace(
        MarketMakerBot bot,
        decimal currentPrice,
        int stepsCount = 20)
        => GetOrdersToPlaceCore(bot, currentPrice, stepsCount, (_) => true);

    public List<CreateMarketOrderCommand> GetOrdersToPlace(
        MarketMakerBot bot,
        decimal currentPrice,
        List<Order> existingOrders,
        int stepsCount = 20)
        => GetOrdersToPlaceCore(bot, currentPrice, stepsCount,
            level => !HasOrderInRange(existingOrders, level.Lower, level.Upper, level.IsBuy));

    public List<CreateMarketOrderCommand> GetOrdersToPlace(
        MarketMakerBot bot,
        decimal currentPrice,
        IReadOnlyCollection<PlacedOrderLevel> existingLevels,
        int stepsCount = 20)
        => GetOrdersToPlaceCore(bot, currentPrice, stepsCount,
            level => !HasLevelInRange(existingLevels, level.Lower, level.Upper, level.IsBuy));

    private List<CreateMarketOrderCommand> GetOrdersToPlaceCore(
        MarketMakerBot bot,
        decimal currentPrice,
        int stepsCount,
        Func<GridLevel, bool> isFree)
    {
        var commands = new List<CreateMarketOrderCommand>();

        if (bot.Role == MarketMakerRole.Buyer)
        {
            var grid = FixedGridEngine.GetGridBelowPrice(currentPrice, stepsCount + 1);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var level = new GridLevel(grid[i + 1], grid[i], IsBuy: true);

                if (isFree(level))
                {
                    var price = GetRandomPriceInRange(level.Lower, level.Upper);
                    var spread = Random.Shared.Next(0, 41);
                    var quantity = (int)Math.Max(bot.BasePower * 0.3m * (1 + spread / 100m), 1);

                    commands.Add(new CreateMarketOrderCommand(
                        bot.TraderId,
                        "купить",
                        bot.Symbol,
                        quantity,
                        price
                    ));
                }
            }
        }
        else if (bot.Role == MarketMakerRole.Seller)
        {
            var grid = FixedGridEngine.GetGridAbovePrice(currentPrice, stepsCount);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var level = new GridLevel(grid[i], grid[i + 1], IsBuy: false);

                if (isFree(level))
                {
                    var price = GetRandomPriceInRange(level.Lower, level.Upper);
                    var spread = Random.Shared.Next(0, 41);
                    var quantity = (int)Math.Max(bot.BasePower * 0.3m * (1 + spread / 100m), 1);

                    commands.Add(new CreateMarketOrderCommand(
                        bot.TraderId,
                        "продать",
                        bot.Symbol,
                        quantity,
                        price
                    ));
                }
            }
        }

        return commands;
    }

    private sealed record GridLevel(decimal Lower, decimal Upper, bool IsBuy);

    private decimal GetRandomPriceInRange(decimal lowerBound, decimal upperBound)
    {
        var min = Math.Min(lowerBound, upperBound);
        var max = Math.Max(lowerBound, upperBound);
        var range = max - min;

        return min + (decimal)_random.NextDouble() * range;
    }

    private static bool HasOrderInRange(List<Order> orders, decimal lowerBound, decimal upperBound, bool isBuy)
    {
        var min = Math.Min(lowerBound, upperBound);
        var max = Math.Max(lowerBound, upperBound);

        return orders.Any(o =>
            o.Price >= min &&
            o.Price <= max &&
            o.IsActive() &&
            (isBuy ? o.IsLong() : o.IsShort()));
    }

    private static bool HasLevelInRange(IReadOnlyCollection<PlacedOrderLevel> levels, decimal lowerBound, decimal upperBound, bool isBuy)
    {
        var min = Math.Min(lowerBound, upperBound);
        var max = Math.Max(lowerBound, upperBound);

        return levels.Any(l =>
            l.Price >= min &&
            l.Price <= max &&
            l.IsBuy == isBuy);
    }
}