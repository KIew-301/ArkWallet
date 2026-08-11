using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.Entities;

/// <summary>
/// Статус слота майнинг-машины
/// </summary>
public enum MiningMachineSlotStatus
{
    /// <summary>Майнит сейчас</summary>
    Active,

    /// <summary>Простаивает</summary>
    Passive,

    /// <summary>Переключается на другой токен</summary>
    Switching,

    /// <summary>Продана</summary>
    Sold
}

/// <summary>
/// Слот майнинг-машины, принадлежащий трейдеру
/// </summary>
internal class MiningMachineSlot
{
    public long Id { get; private set; }
    public long TraderId { get; private set; }
    public long MiningMachineId { get; private set; }
    public string? TokenId { get; private set; }
    public long? MachineRuleId { get; private set; }
    public long? MiningGlobalRuleId { get; private set; }
    public MiningMachineSlotStatus Status { get; private set; }
    public DateTime? StartSwitchingDateTime { get; private set; }
    public DateTime? EndSwitchingDateTime { get; private set; }
    public decimal TokensAmountCollected { get; private set; }
    public decimal Cost { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SoldAt { get; private set; }

    public virtual MiningMachine? MiningMachine { get; set; }
    public virtual MiningMachineRule? MachineRule { get; set; }
    public virtual MiningGlobalRule? MiningGlobalRule { get; set; }
    public virtual CharacterToken? Token { get; set; }

    public static MiningMachineSlot Create(long traderId, long miningMachineId, decimal cost, DateTime createdAt)
    {
        if (cost <= 0)
            throw new DomainException("Цена продажи должна быть больше нуля");

        return new MiningMachineSlot
        {
            TraderId = traderId,
            MiningMachineId = miningMachineId,
            Status = MiningMachineSlotStatus.Passive,
            Cost = cost,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Запускает переключение слота на другой токен. Токен и правила фиксируются сразу,
    /// но майнинг начнётся только после завершения переключения.
    /// </summary>
    public void SwitchTargetToken(
        long traderId,
        string symbol,
        long machineRuleId,
        long globalRuleId,
        int switchingTime,
        DateTime now)
    {
        if (TraderId != traderId)
            throw new DomainException("Трейдер не владеет данной машиной");
        if (Status == MiningMachineSlotStatus.Sold)
            throw new DomainException("Машина уже продана");

        TokenId = symbol;
        MachineRuleId = machineRuleId;
        MiningGlobalRuleId = globalRuleId;
        StartSwitchingDateTime = now;
        EndSwitchingDateTime = now.AddMinutes(switchingTime);
        Status = MiningMachineSlotStatus.Switching;
    }

    /// <summary>Завершает переключение: слот переходит в статус active</summary>
    public void CompleteSwitching()
    {
        if (Status != MiningMachineSlotStatus.Switching)
            throw new DomainException("Слот не находится в статусе переключения");

        StartSwitchingDateTime = null;
        EndSwitchingDateTime = null;
        Status = MiningMachineSlotStatus.Active;
    }

    /// <summary>Добавляет накопленные токены (дробное количество)</summary>
    public void AddTokens(decimal cash)
        => TokensAmountCollected += cash;

    /// <summary>Забирает целую часть накопленных токенов, дробную оставляет на слоте</summary>
    public int CollectWholeTokens()
    {
        var whole = (int)TokensAmountCollected;
        TokensAmountCollected -= whole;
        return whole;
    }

    /// <summary>Продаёт слот: зачисляет выручку и переводит в статус sold</summary>
    public void Sell(long traderId, DateTime soldAt)
    {
        if (TraderId != traderId)
            throw new DomainException("Трейдер не владеет данной машиной");
        if (Status == MiningMachineSlotStatus.Sold)
            throw new DomainException("Машина уже продана");

        Status = MiningMachineSlotStatus.Sold;
        SoldAt = soldAt;
    }
}
