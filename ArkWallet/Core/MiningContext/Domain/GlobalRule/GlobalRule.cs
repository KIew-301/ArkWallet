using ArkWallet.Core.General.Domain.Exceptions;

namespace ArkWallet.Core.MiningContext.Domain.GlobalRule;

/// <summary>
/// Агрегат глобального правила майнинга: валидация, сдвиг коэффициентов, обновление.
/// Data-сущность <see cref="Infrastructure.Data.MiningGlobalRule"/> делегирует вычисления сюда (правило 18).
/// </summary>
public class GlobalRule
{
    public long Id { get; private set; }
    public string TokenSymbol { get; private set; } = string.Empty;
    public decimal CurrentCoefficient { get; private set; }
    public decimal FutureCoefficient { get; private set; }
    public decimal BaseTokenMiningSpeed { get; private set; }

    private GlobalRule() { }

    public static GlobalRule Create(
        string tokenSymbol,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Токен не указан");
        if (currentCoefficient <= 0)
            throw new DomainException("Текущий коэффициент должен быть больше нуля");
        if (futureCoefficient <= 0)
            throw new DomainException("Будущий коэффициент должен быть больше нуля");
        if (baseTokenMiningSpeed <= 0)
            throw new DomainException("Базовая скорость должна быть больше нуля");

        return new GlobalRule
        {
            TokenSymbol = tokenSymbol,
            CurrentCoefficient = currentCoefficient,
            FutureCoefficient = futureCoefficient,
            BaseTokenMiningSpeed = baseTokenMiningSpeed
        };
    }

    public static GlobalRule Load(
        string tokenSymbol,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
    {
        return new GlobalRule
        {
            TokenSymbol = tokenSymbol,
            CurrentCoefficient = currentCoefficient,
            FutureCoefficient = futureCoefficient,
            BaseTokenMiningSpeed = baseTokenMiningSpeed
        };
    }

    /// <summary>
    /// Обновляет переданные поля правила. Null-значения игнорируются.
    /// Коэффициенты задаются парой — передача только одного вызывает ошибку.
    /// </summary>
    public void Update(
        decimal? currentCoefficient = null,
        decimal? futureCoefficient = null,
        decimal? baseTokenMiningSpeed = null)
    {
        var coefChanged = currentCoefficient.HasValue || futureCoefficient.HasValue;
        if (coefChanged && !(currentCoefficient.HasValue && futureCoefficient.HasValue))
            throw new DomainException("Коэффициенты задаются парой: текущий и будущий");

        if (currentCoefficient.HasValue)
        {
            if (currentCoefficient.Value <= 0)
                throw new DomainException("Текущий коэффициент должен быть больше нуля");
            if (futureCoefficient!.Value <= 0)
                throw new DomainException("Будущий коэффициент должен быть больше нуля");
            CurrentCoefficient = currentCoefficient.Value;
            FutureCoefficient = futureCoefficient.Value;
        }

        if (baseTokenMiningSpeed.HasValue)
        {
            if (baseTokenMiningSpeed.Value <= 0)
                throw new DomainException("Базовая скорость должна быть больше нуля");
            BaseTokenMiningSpeed = baseTokenMiningSpeed.Value;
        }
    }

    /// <summary>Сдвигает коэффициенты: текущий становится будущим, будущий обновляется</summary>
    public void AdvanceCoefficient(decimal newFutureCoefficient)
    {
        if (newFutureCoefficient <= 0)
            throw new DomainException("Будущий коэффициент должен быть больше нуля");

        CurrentCoefficient = FutureCoefficient;
        FutureCoefficient = newFutureCoefficient;
    }

    /// <summary>Обновляет коэффициенты токена напрямую (текущий и будущий)</summary>
    public void UpdateCoefficients(decimal currentCoefficient, decimal futureCoefficient)
    {
        if (currentCoefficient <= 0)
            throw new DomainException("Текущий коэффициент должен быть больше нуля");
        if (futureCoefficient <= 0)
            throw new DomainException("Будущий коэффициент должен быть больше нуля");

        CurrentCoefficient = currentCoefficient;
        FutureCoefficient = futureCoefficient;
    }

    public void UpdateBaseTokenMiningSpeed(decimal baseTokenMiningSpeed)
    {
        if (baseTokenMiningSpeed <= 0)
            throw new DomainException("Базовая скорость должна быть больше нуля");

        BaseTokenMiningSpeed = baseTokenMiningSpeed;
    }
}