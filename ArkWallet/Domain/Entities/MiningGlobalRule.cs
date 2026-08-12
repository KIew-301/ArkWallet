using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.Entities;

/// <summary>
/// Глобальное правило майнинга токена: коэффициенты и базовая скорость добычи
/// </summary>
internal class MiningGlobalRule
{
    public long Id { get; private set; }
    public string TokenId { get; private set; } = string.Empty;
    public decimal CurrentCoefficient { get; private set; }
    public decimal FutureCoefficient { get; private set; }
    public decimal BaseMiningSpeed { get; private set; }

    public virtual CharacterToken? CharacterToken { get; set; }

    public static MiningGlobalRule Create(
        string tokenId,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseMiningSpeed)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            throw new DomainException("Токен не указан");
        if (currentCoefficient <= 0)
            throw new DomainException("Текущий коэффициент должен быть больше нуля");
        if (futureCoefficient <= 0)
            throw new DomainException("Будущий коэффициент должен быть больше нуля");
        if (baseMiningSpeed <= 0)
            throw new DomainException("Базовая скорость должна быть больше нуля");

        return new MiningGlobalRule
        {
            TokenId = tokenId,
            CurrentCoefficient = currentCoefficient,
            FutureCoefficient = futureCoefficient,
            BaseMiningSpeed = baseMiningSpeed
        };
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

    public void UpdateBaseMiningSpeed(decimal baseMiningSpeed)
    {
        if (baseMiningSpeed <= 0)
            throw new DomainException("Базовая скорость должна быть больше нуля");

        BaseMiningSpeed = baseMiningSpeed;
    }
}
