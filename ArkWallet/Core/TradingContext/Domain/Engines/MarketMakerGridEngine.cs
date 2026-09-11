using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;

namespace ArkWallet.Core.TradingContext.Domain.Engines;

internal class MarketMakerGridEngine(FixedGridEngine fixedGridEngine)
{
    private readonly Random _random = new();

    public List<CreateMarketOrderCommand> GetOrdersToPlace(
        MarketMaker bot,
        decimal currentPrice,
        List<Order> existingOrders,
        int stepsCount = 20)
    {
        var commands = new List<CreateMarketOrderCommand>();

        if (bot.Role == MarketMakerRole.Buyer)
        {
            var grid = fixedGridEngine.GetGridBelowPrice(currentPrice, stepsCount + 1);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var lower = grid[i + 1];
                var upper = grid[i];

                if (!HasOrderInRange(existingOrders, lower, upper, isBuy: true))
                {
                    var price = GetRandomPriceInRange(lower, upper);
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
            var grid = fixedGridEngine.GetGridAbovePrice(currentPrice, stepsCount);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var lower = grid[i];
                var upper = grid[i + 1];

                if (!HasOrderInRange(existingOrders, lower, upper, isBuy: false))
                {
                    var price = GetRandomPriceInRange(lower, upper);
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
}