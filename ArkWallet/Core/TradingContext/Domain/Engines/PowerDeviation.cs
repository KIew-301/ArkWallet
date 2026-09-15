namespace ArkWallet.Core.TradingContext.Domain.Engines;

/// <summary>Расчёт коэффициента отклонения мощности на основе процентного падения или роста цены за день, неделю и месяц.</summary>
public static class PowerDeviation
{
    /// <summary>Вычисляет коэффициент мощности на основе процентов падения за день, неделю и месяц. Возвращает значение в диапазоне [1, 1.25].</summary>
    /// <param name="dayDropPercent">Процент падения цены за день.</param>
    /// <param name="weekDropPercent">Процент падения цены за неделю.</param>
    /// <param name="monthDropPercent">Процент падения цены за месяц.</param>
    public static decimal CalculateCoefficient(decimal dayDropPercent, decimal weekDropPercent, decimal monthDropPercent)
    {
        var result = 1m
            + (dayDropPercent > 0 ? 0.01m * (dayDropPercent / 4m) : 0m)
            + (weekDropPercent > 0 ? 0.01m * (weekDropPercent / 6m) : 0m)
            + (monthDropPercent > 0 ? 0.01m * (monthDropPercent / 9m) : 0m);

        return Math.Clamp(result, 1m, 1.25m);
    }

    /// <summary>Вычисляет процент падения между двумя ценами (положительное число при падении).</summary>
    /// <param name="first">Первая цена.</param>
    /// <param name="last">Вторая цена.</param>
    public static decimal PercentDrop(decimal first, decimal last) => first > 0 ? ((first - last) / first) * 100m : 0m;

    /// <summary>Вычисляет процент роста между двумя ценами (положительное число при росте).</summary>
    /// <param name="first">Первая цена.</param>
    /// <param name="last">Вторая цена.</param>
    public static decimal PercentRise(decimal first, decimal last) => first > 0 ? ((last - first) / first) * 100m : 0m;
}
