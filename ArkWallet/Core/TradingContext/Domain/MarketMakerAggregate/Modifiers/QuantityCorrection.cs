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
            decimal adverse;
            if (order.Direction == "купить")
                adverse = deviationPercent > 0 ? deviationPercent : 0m;
            else
                adverse = deviationPercent < 0 ? -deviationPercent : 0m;

            decimal reduce;
            if (reductionPerPercentPoint > 0)
            {
                reduce = Math.Clamp(adverse * reductionPerPercentPoint, 0m, 100m);
            }
            else
            {
                var beyondDeadBand = Math.Max(adverse - deadBandPercent, 0m);
                reduce = Math.Clamp(beyondDeadBand * coefficient, 0m, 100m);
            }
            var quantity = (int)(order.Quantity * (1m - reduce / 100m));
            if (quantity >= 1)
                result.Add(order with { Quantity = quantity });
        }

        return result;
    }
}
