namespace ArkWallet.Core.TradingContext.Domain.Engines;

public static class PowerDeviation
{
    public static decimal CalculateCoefficient(decimal dayDropPercent, decimal weekDropPercent, decimal monthDropPercent)
    {
        var result = 1m
            + (dayDropPercent > 0 ? 0.01m * (dayDropPercent / 4m) : 0m)
            + (weekDropPercent > 0 ? 0.01m * (weekDropPercent / 6m) : 0m)
            + (monthDropPercent > 0 ? 0.01m * (monthDropPercent / 9m) : 0m);

        return Math.Clamp(result, 1m, 1.25m);
    }

    public static decimal PercentDrop(decimal first, decimal last) => first > 0 ? ((first - last) / first) * 100m : 0m;

    public static decimal PercentRise(decimal first, decimal last) => first > 0 ? ((last - first) / first) * 100m : 0m;
}
