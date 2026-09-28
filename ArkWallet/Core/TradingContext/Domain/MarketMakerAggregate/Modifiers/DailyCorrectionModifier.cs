namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Корректировка за день: снижает quantity при неблагоприятном движении цены за сутки
/// (рост — для «купить», падение — для «продать»). Мёртвая зона 0–10%: ниже 10% отклонения ничего не режет;
/// с 10 до 50% — плавное линейное снижение до 100%.
/// </summary>
internal sealed class DailyCorrectionModifier : IPlanModify
{
    private const decimal DeadBandPercent = 10m;
    private const decimal FullReductionPercent = 50m;

    public MarketDataMasks RequiredMarketData => MarketDataMasks.CurrentPrice | MarketDataMasks.DayAgoPrice;

    public IReadOnlyCollection<CreateMarketOrderCommand> Build(
        MarketMakerBot bot, MarketConditions market, IReadOnlyCollection<CreateMarketOrderCommand> currentPlan)
    {
        if (market.DayAgoPrice is not { } dayAgoPrice || dayAgoPrice <= 0)
            return currentPlan;

        var deviationPercent = (market.CurrentPrice - dayAgoPrice) / dayAgoPrice * 100m;
        return QuantityCorrection.Apply(currentPlan, deviationPercent, reductionPerPercentPoint: 0m,
            DeadBandPercent, FullReductionPercent);
    }
}
