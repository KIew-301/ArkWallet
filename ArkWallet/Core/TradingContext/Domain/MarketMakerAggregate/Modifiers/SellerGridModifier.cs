using ArkWallet.Core.TradingContext.Domain.Engines;

namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Источник ордеров сетки продавца: делегирует движку сетки (роль Seller).</summary>
internal sealed class SellerGridModifier : IPlanModify
{
    private const int GridSteps = 20;

    private readonly MarketMakerGridEngine _engine = new();

    public MarketDataMasks RequiredMarketData => MarketDataMasks.CurrentPrice | MarketDataMasks.ExistingLevels;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (bot.Role != MarketMakerRole.Seller)
            return currentPlan;

        var orders = _engine.GetOrdersToPlace(bot, market.CurrentPrice, market.ExistingLevels, GridSteps);
        return currentPlan.Concat(orders).ToList();
    }
}
