using ArkWallet.Core.TradingContext.Domain.Engines;

namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Источник рыночного ордера: логика MarketMakerOrderEngine (Buyer — покупка, Seller — продажа).</summary>
internal sealed class MarketOrderModifier : IPlanModify
{
    public MarketDataMasks RequiredMarketData => MarketDataMasks.CurrentPrice;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (bot.Role == MarketMakerRole.Waller)
            return currentPlan;

        var order = MarketMakerOrderEngine.BuildActiveOrder(bot, market.CurrentPrice);
        return currentPlan.Append(order).ToList();
    }
}
