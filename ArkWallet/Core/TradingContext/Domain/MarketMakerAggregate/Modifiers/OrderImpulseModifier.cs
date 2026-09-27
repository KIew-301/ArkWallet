namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Импульс: один случайный коэффициент на весь план — 1.0 (90%), 1.65 (7.25%), 4.75 (1.85%), 7.25 (0.75%), 10.55 (0.5%).</summary>
internal sealed class OrderImpulseModifier : IPlanModify
{
    public MarketDataMask RequiredMarketData => MarketDataMask.None;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (currentPlan.Count == 0)
            return currentPlan;

        var roll = Random.Shared.NextDouble();
        var factor = roll < 0.90 ? 1.0m
            : roll < 0.9725 ? 1.65m
            : roll < 0.991 ? 4.75m
            : roll < 0.9985 ? 7.25m
            : 10.55m;
        if (factor == 1.0m)
            return currentPlan;

        return currentPlan
            .Select(order => order with { Quantity = (int)Math.Max(order.Quantity * factor, 1m) })
            .ToList();
    }
}
