using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.TradingContext.Domain.Engines;

internal class MarketMakerOrderEngine
{
    public CreateMarketOrderCommand BuildActiveOrder(MarketMaker bot, decimal currentPrice)
    {
        var isBuyer = bot.Role == MarketMakerRole.Buyer;
        var deviation = 0.2m;

        var targetPrice = isBuyer
            ? currentPrice * (1 + deviation)
            : currentPrice * (1 - deviation);

        var effectiveCoeff = GetEffectiveCoeff(bot.PowerDeviationCoeff);

        var minPower = (int)(bot.BasePower * 0.5m);
        var maxPower = (int)(bot.BasePower + minPower);

        var quantity = (int)Math.Max(Random.Shared.Next(minPower, maxPower) * effectiveCoeff, 1m);
        var direction = isBuyer ? "купить" : "продать";

        return new CreateMarketOrderCommand(
            bot.TraderId,
            direction,
            bot.Symbol,
            quantity,
            FixedGridEngine.RoundToStep(targetPrice)
        );
    }

    private static decimal GetEffectiveCoeff(decimal baseCoeff)
    {
        var roll = Random.Shared.NextDouble();
        return roll switch
        {
            < 0.87 => baseCoeff,
            < 0.95 => 1.5m,
            < 0.99 => 3.2m,
            _ => 5.5m
        };
    }
}
