using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.TradingContext.Domain.Engines;

internal static class MarketMakerOrderEngine
{
    public static CreateMarketOrderCommand BuildActiveOrder(MarketMakerBot bot, decimal currentPrice, decimal quantityMultiplier = 1m)
    {
        var isBuyer = bot.Role == MarketMakerRole.Buyer;
        var deviation = 0.2m;

        var targetPrice = isBuyer
            ? currentPrice * (1 + deviation)
            : currentPrice * (1 - deviation);

        var quantity = (int)Math.Max(bot.ActivePower * quantityMultiplier, 1m);
        var direction = isBuyer ? "купить" : "продать";

        return new CreateMarketOrderCommand(
            bot.TraderId,
            direction,
            bot.Symbol,
            quantity,
            FixedGridEngine.RoundToStep(targetPrice)
        );
    }
}
