using ArkWallet.Core.MiningContext.Domain.GlobalRule;

namespace ArkWallet.Infrastructure.Data;

/// <summary>
/// Глобальное правило майнинга токена: коэффициенты и базовая скорость добычи токена.
/// Данные-сущность: только операции создания, обновления и копирования (правило 18).
/// </summary>
internal class MiningGlobalRule : EntityData
{
    public long Id { get; }
    public string TokenId { get; private set; } = string.Empty;
    public decimal CurrentCoefficient { get; private set; }
    public decimal FutureCoefficient { get; private set; }
    public decimal BaseTokenMiningSpeed { get; private set; }

    public virtual CharacterToken? CharacterToken { get; set; }

    public static MiningGlobalRule Create(
        string tokenId,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
    {
        var rule = GlobalRule.Create(tokenId, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);
        return new MiningGlobalRule
        {
            TokenId = tokenId,
            CurrentCoefficient = rule.CurrentCoefficient,
            FutureCoefficient = rule.FutureCoefficient,
            BaseTokenMiningSpeed = rule.BaseTokenMiningSpeed
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
        var rule = GlobalRule.Load(TokenId, CurrentCoefficient, FutureCoefficient, BaseTokenMiningSpeed);
        rule.Update(currentCoefficient, futureCoefficient, baseTokenMiningSpeed);

        CurrentCoefficient = rule.CurrentCoefficient;
        FutureCoefficient = rule.FutureCoefficient;
        BaseTokenMiningSpeed = rule.BaseTokenMiningSpeed;
    }

    /// <summary>Создаёт независимую копию правила</summary>
    public MiningGlobalRule Copy()
    {
        return new MiningGlobalRule
        {
            TokenId = TokenId,
            CurrentCoefficient = CurrentCoefficient,
            FutureCoefficient = FutureCoefficient,
            BaseTokenMiningSpeed = BaseTokenMiningSpeed
        };
    }
}