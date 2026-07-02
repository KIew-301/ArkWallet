using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Domain.Engines;

internal class MarketMakerGridEngine
{
    private readonly Random _random = new();

    public List<CreateOrderCommand> GetOrdersToPlace(
        MarketMakerBot bot,
        decimal currentPrice,
        List<TradeOrder> existingOrders,
        int stepsCount = 20,
        decimal minPricePercent = 0.8m,
        decimal maxPricePercent = 1.2m)
    {
        var commands = new List<CreateOrderCommand>();

        if (bot.Role == BotRole.Buyer)
        {
            var grid = GenerateGrid(currentPrice, stepsCount, minPricePercent, true);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var lower = grid[i + 1];
                var upper = grid[i];

                if (!HasOrderInRange(existingOrders, lower, upper, "купить"))
                {
                    var price = GetRandomPriceInRange(lower, upper);
                    var quantity = (int)Math.Max(bot.BasePower * 0.3m, 1);

                    commands.Add(new CreateOrderCommand(
                        bot.TraderId,
                        "купить",
                        bot.Symbol,
                        quantity,
                        Math.Round(price, 2)
                    ));
                }
            }
        }
        else if (bot.Role == BotRole.Seller)
        {
            var grid = GenerateGrid(currentPrice, stepsCount, maxPricePercent, false);

            for (int i = 0; i < grid.Count - 1; i++)
            {
                var lower = grid[i];
                var upper = grid[i + 1];

                if (!HasOrderInRange(existingOrders, lower, upper, "продать"))
                {
                    var price = GetRandomPriceInRange(lower, upper);
                    var quantity = (int)Math.Max(bot.BasePower * 0.3m, 1);

                    commands.Add(new CreateOrderCommand(
                        bot.TraderId,
                        "продать",
                        bot.Symbol,
                        quantity,
                        Math.Round(price, 2)
                    ));
                }
            }
        }

        return commands;
    }

    private List<decimal> GenerateGrid(decimal currentPrice, int stepsCount, decimal limitPercent, bool isBuyer)
    {
        var grid = new List<decimal>();
        var price = currentPrice;
        var limitPrice = currentPrice * limitPercent;

        for (int i = 0; i <= stepsCount; i++)
        {
            var step = price * 0.001m;
            price = isBuyer ? price - step : price + step;

            if (isBuyer && price < limitPrice)
                break;
            if (!isBuyer && price > limitPrice)
                break;

            grid.Add(price);
        }

        return grid;
    }

    private decimal GetRandomPriceInRange(decimal lowerBound, decimal upperBound)
    {
        var min = Math.Min(lowerBound, upperBound);
        var max = Math.Max(lowerBound, upperBound);
        var range = max - min;

        return min + (decimal)_random.NextDouble() * range;
    }

    private bool HasOrderInRange(List<TradeOrder> orders, decimal lowerBound, decimal upperBound, string direction)
    {
        var min = Math.Min(lowerBound, upperBound);
        var max = Math.Max(lowerBound, upperBound);

        return orders.Any(o =>
            o.Price >= min &&
            o.Price <= max &&
            o.IsActive() &&
            (direction == "купить" ? o.IsLong() : o.IsShort()));
    }
}