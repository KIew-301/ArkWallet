using ArkWallet.Core.TradingContext.Domain.Engines;

namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Источник ордеров стены (Waller): по уровням WallBlockerEngine, объём = BasePower × 8.</summary>
internal sealed class WallGridModifier : IPlanModify
{
    private const decimal WallMultiplier = 8m;

    private readonly WallBlockerEngine _engine = new();

    public MarketDataMasks RequiredMarketData => MarketDataMasks.CurrentPrice;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (bot.Role != MarketMakerRole.Waller)
            return currentPlan;

        var levels = _engine.GetLevels(market.CurrentPrice);
        if (levels.Count == 0)
            return currentPlan;

        var commands = levels.Select(level =>
        {
            var quantity = (int)Math.Max(bot.BasePower * WallMultiplier, 1m);

            return new CreateMarketOrderCommand(
                bot.TraderId, level.Direction, bot.Symbol, quantity, Math.Round(level.Price, 2));
        });

        return currentPlan.Concat(commands).ToList();
    }
}
