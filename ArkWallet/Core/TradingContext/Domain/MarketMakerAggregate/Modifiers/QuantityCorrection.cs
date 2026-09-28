namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Хелпер коррекций quantity по отклонению цены: для ордера «купить» снижает при росте цены,
/// для «продать» — при падении; снижение начинается после мёртвой зоны (deadBandPercent) и достигает
/// 100% на fullReductionPercent; кламперируется в [0, 100]%, ордера с quantity &lt; 1 отбрасываются.
/// </summary>
internal static class QuantityCorrection
{
    public static IReadOnlyCollection<CreateMarketOrderCommand> Apply(
        IReadOnlyCollection<CreateMarketOrderCommand> plan,
        decimal deviationPercent,
        decimal reductionPerPercentPoint,
        decimal deadBandPercent = 0m,
        decimal fullReductionPercent = 50m)
    {
        var span = fullReductionPercent - deadBandPercent;
        var coefficient = span > 0 ? 100m / span : 0m;

        var result = new List<CreateMarketOrderCommand>(plan.Count);

        foreach (var order in plan)
        {
            var adverse = CalculateAdverse(order.Direction, deviationPercent);
            var reduce = CalculateReduce(adverse, reductionPerPercentPoint, deadBandPercent, coefficient);
            var quantity = (int)(order.Quantity * (1m - reduce / 100m));
            if (quantity >= 1)
                result.Add(order with { Quantity = quantity });
        }

        return result;
    }

    private static decimal CalculateAdverse(string direction, decimal deviationPercent)
        => direction == "купить"
            ? Math.Max(deviationPercent, 0m)
            : Math.Max(-deviationPercent, 0m);

    private static decimal CalculateReduce(
        decimal adverse, decimal reductionPerPercentPoint, decimal deadBandPercent, decimal coefficient)
    {
        if (reductionPerPercentPoint > 0)
            return Math.Clamp(adverse * reductionPerPercentPoint, 0m, 100m);

        var beyondDeadBand = Math.Max(adverse - deadBandPercent, 0m);
        return Math.Clamp(beyondDeadBand * coefficient, 0m, 100m);
    }
}
