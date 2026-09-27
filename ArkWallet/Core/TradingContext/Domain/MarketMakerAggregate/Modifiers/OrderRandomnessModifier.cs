namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Случайность ордеров: умножает quantity на равномерный коэффициент из [0.8, 1.2).</summary>
internal sealed class OrderRandomnessModifier : IPlanModify
{
    public MarketDataMask RequiredMarketData => MarketDataMask.None;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (currentPlan.Count == 0)
            return currentPlan;

        var factor = 0.8m + (decimal)Random.Shared.NextDouble() * 0.4m;
        return currentPlan
            .Select(order => order with { Quantity = (int)Math.Max(order.Quantity * factor, 1m) })
            .ToList();
    }
}
