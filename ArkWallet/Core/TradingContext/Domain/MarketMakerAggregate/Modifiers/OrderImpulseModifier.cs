namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Импульс: один случайный коэффициент на весь план — 1.0 (90%), 1.65 (7.25%), 4.75 (1.85%), 7.25 (0.75%), 10.55 (0.5%).</summary>
internal sealed class OrderImpulseModifier : IPlanModify
{
    public MarketDataMasks RequiredMarketData => MarketDataMasks.None;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (currentPlan.Count == 0)
            return currentPlan;

        var roll = Random.Shared.NextDouble();
        decimal factor;
        if (roll < 0.90)
            factor = 1.0m;
        else if (roll < 0.9725)
            factor = 1.65m;
        else if (roll < 0.991)
            factor = 4.75m;
        else if (roll < 0.9985)
            factor = 7.25m;
        else
            factor = 10.55m;
        if (factor == 1.0m)
            return currentPlan;

        return currentPlan
            .Select(order => order with { Quantity = (int)Math.Max(order.Quantity * factor, 1m) })
            .ToList();
    }
}
