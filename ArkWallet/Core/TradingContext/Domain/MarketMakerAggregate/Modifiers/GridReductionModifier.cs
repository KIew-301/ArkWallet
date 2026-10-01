namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Случайность размера сеточных ордеров buyer/seller: quantity каждого ордера умножается
/// на равномерный случайный фактор из диапазона [0.5; 2.5] — заявки то слабее, то заметно толще номинала.
/// </summary>
internal sealed class GridReductionModifier : IPlanModify
{
    private const decimal MinFactor = 0.5m;
    private const decimal MaxFactor = 2.5m;

    public MarketDataMasks RequiredMarketData => MarketDataMasks.None;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (currentPlan.Count == 0)
            return currentPlan;

        var factor = MinFactor + (decimal)Random.Shared.NextDouble() * (MaxFactor - MinFactor);

        return currentPlan
            .Select(order => order with { Quantity = (int)Math.Max(order.Quantity * factor, 1m) })
            .ToList();
    }
}