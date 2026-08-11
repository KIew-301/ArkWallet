using System.Security.Cryptography;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.Engines;

/// <summary>
/// Статус токена для майнинга
/// </summary>
public enum MiningStatus
{
    /// <summary>Прибыль ниже среднего уровня</summary>
    Unprofitable,

    /// <summary>Прибыль на среднем уровне</summary>
    Stable,

    /// <summary>Прибыль выше среднего уровня</summary>
    Profitable
}

/// <summary>
/// Движок бизнес-логики системы майнинга: формулы расчёта добычи, прибыли и статусов
/// </summary>
internal class MiningEngine
{
    public const int MaxMachinesPerTrader = 10;
    public const decimal MinCoefficient = 0.9m;
    public const decimal MaxCoefficient = 1.1m;
    public const decimal DefaultBaseMiningSpeedDivisor = 50m;

#pragma warning disable S2325, CA1822 // Экземплярный метод: движок инжектится через DI (как FixedGridEngine)
    /// <summary>Прибыль за период: глобальный коэффициент * коэффициент машины * время * базовая скорость</summary>
    public decimal CalculateCash(
        decimal ruleCoeff,
        decimal machineCoeff,
        decimal timingCoeff,
        decimal baseMiningSpeed)
    {
        if (timingCoeff <= 0)
            throw new DomainException("Коэффициент времени должен быть больше нуля");

        return ruleCoeff * machineCoeff * timingCoeff * baseMiningSpeed;
    }

    /// <summary>Скорость майнинга: глобальный коэффициент * коэффициент машины * базовая скорость</summary>
    public decimal CalculateMiningSpeed(
        decimal ruleCoeff,
        decimal machineCoeff,
        decimal baseMiningSpeed)
        => ruleCoeff * machineCoeff * baseMiningSpeed;

    /// <summary>Прибыль: скорость майнинга * текущая цена токена</summary>
    public decimal CalculateProfit(decimal miningSpeed, decimal currentPrice)
        => miningSpeed * currentPrice;

    /// <summary>Базовая прибыль: базовая скорость майнинга * текущая цена токена</summary>
    public decimal CalculateBaseProfit(decimal baseMiningSpeed, decimal currentPrice)
        => baseMiningSpeed * currentPrice;

    /// <summary>Базовая скорость майнинга: константа / текущая цена токена</summary>
    public decimal CalculateBaseMiningSpeed(decimal currentPrice)
        => DefaultBaseMiningSpeedDivisor / currentPrice;

    /// <summary>
    /// Процент завершения переключения. Для не-switching слотов всегда 100,
    /// для switching — доля прошедшего времени от start до end.
    /// </summary>
    public decimal CalculateSwitchingPercent(DateTime now, DateTime? start, DateTime? end)
    {
        if (start == null || end == null || end.Value <= start.Value)
            return 100m;

        if (now >= end.Value)
            return 100m;

        if (now <= start.Value)
            return 0m;

        var elapsed = (now - start.Value).TotalMinutes;
        var total = (end.Value - start.Value).TotalMinutes;
        var percent = (decimal)(elapsed / total) * 100m;
        return Math.Clamp((decimal)percent, 0m, 100m);
    }

    /// <summary>
    /// Статус по позиции значения относительно диапазона:
    /// менее 0.5 — unprofitable, от 0.5 до 0.75 — stable, выше 0.75 — profitable.
    /// </summary>
    public MiningStatus CalculateStatus(decimal value, decimal minValue, decimal maxValue)
    {
        var position = CalculatePosition(value, minValue, maxValue);

        if (position < 0.5m)
            return MiningStatus.Unprofitable;
        if (position <= 0.75m)
            return MiningStatus.Stable;

        return MiningStatus.Profitable;
    }

    /// <summary>Позиция значения в диапазоне [min, max]: (value - min) / (max - min)</summary>
    public decimal CalculatePosition(decimal value, decimal minValue, decimal maxValue)
    {
        if (maxValue <= minValue)
            return 0.5m;

        return (value - minValue) / (maxValue - minValue);
    }

    /// <summary>Целая часть накопленных токенов (дробная остаётся на слоте)</summary>
    public int CollectWholeTokens(decimal tokensAmountCollected)
        => (int)tokensAmountCollected;

    /// <summary>Случайный коэффициент майнинга в диапазоне [0.9, 1.1]</summary>
    public decimal NextCoefficient()
    {
        var next = RandomNumberGenerator.GetInt32(0, 1_000_000_001);
        return MinCoefficient + (MaxCoefficient - MinCoefficient) * next / 1_000_000_000m;
    }

    /// <summary>
    /// Коэффициент времени: количество минут с последнего расчёта (с неполными минутами),
    /// минимум 1. Например, 2 минуты 20 секунд — 2.33.
    /// </summary>
    public decimal CalculateTimingCoeff(DateTime now, DateTime lastCalculation)
    {
        var minutes = (now - lastCalculation).TotalMinutes;
        if (minutes <= 0)
            return 1m;

        return Math.Round((decimal)minutes, 2);
    }
#pragma warning restore S2325, CA1822
}
