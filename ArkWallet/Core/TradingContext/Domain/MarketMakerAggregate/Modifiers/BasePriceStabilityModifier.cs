namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Модификатор стабильности: от базовой цены (открытие самой первой свечи) снижает quantity;
/// реакция мягче дневной корректировки — 2% снижения за каждые 1.2% отклонения (деление на 1.2).
/// </summary>
internal sealed class BasePriceStabilityModifier : IPlanModify
{
    private const decimal ReductionPerPercent = 2m;
    private const decimal DeviationScaling = 1.2m;

    public MarketDataMask RequiredMarketData => MarketDataMask.CurrentPrice | MarketDataMask.BasePrice;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (market.BasePrice is not { } basePrice || basePrice <= 0)
            return currentPlan;

        var deviationPercent = (market.CurrentPrice - basePrice) / basePrice * 100m;
        return QuantityCorrection.Apply(currentPlan, deviationPercent, ReductionPerPercent / DeviationScaling);
    }
}
