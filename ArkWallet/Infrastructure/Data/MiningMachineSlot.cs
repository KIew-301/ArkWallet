using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.MiningContext.Domain.Machine;

namespace ArkWallet.Infrastructure.Data;

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
/// Слот майнинг-машины, принадлежащий трейдеру.
/// При покупке копирует характеристики каталогной машины и не зависит от неё.
/// Данные-сущность: содержит только операции создания и обновления. Бизнес-логика
/// переключения, накопления, сбора и продажи живёт в доменном агрегате
/// <see cref="Machine"/> (правило 18).
/// </summary>
internal class MiningMachineSlot : EntityData
{
    public long Id { get; }
    public long TraderId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MiningMachineType Type { get; private set; }
    public int SwitchingTime { get; private set; }
    public decimal Efficiency { get; private set; }
    public string Image { get; private set; } = string.Empty;
    public string? TokenId { get; private set; }
    public long? MiningGlobalRuleId { get; private set; }
    public MiningMachineSlotStatus Status { get; private set; }
    public DateTime? StartSwitchingDateTime { get; private set; }
    public DateTime? EndSwitchingDateTime { get; private set; }
    public decimal TokensAmountCollected { get; private set; }
    public decimal Cost { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SoldAt { get; private set; }

    public virtual ICollection<MiningMachineSlotRule> MiningMachineSlotRules { get; set; } = new List<MiningMachineSlotRule>();
    public virtual MiningGlobalRule? MiningGlobalRule { get; set; }
    public virtual CharacterToken? Token { get; set; }

    public static MiningMachineSlot Create(long traderId, MiningMachine machine, decimal cost, DateTime createdAt)
    {
        if (cost <= 0)
            throw new DomainException("Цена продажи должна быть больше нуля");

        var slot = new MiningMachineSlot
        {
            TraderId = traderId,
            Name = machine.Name,
            Type = machine.Type,
            SwitchingTime = machine.SwitchingTime,
            Efficiency = machine.Efficiency,
            Image = machine.Image,
            Status = MiningMachineSlotStatus.Passive,
            Cost = cost,
            CreatedAt = createdAt
        };

        foreach (var rule in machine.MiningMachineRules)
            slot.MiningMachineSlotRules.Add(MiningMachineSlotRule.Copy(rule));

        return slot;
    }

    /// <summary>
    /// Применяет состояние доменной машины к сущности. Сами переходы (переключение,
    /// завершение переключения, накопление, сбор, продажа) выполняются на <see cref="Machine"/>.
    /// </summary>
    public void Update(Machine machine)
    {
        TokenId = machine.TokenSymbol;
        MiningGlobalRuleId = machine.GlobalRuleId;
        Status = machine.Status switch
        {
            MachineStatus.Active => MiningMachineSlotStatus.Active,
            MachineStatus.Passive => MiningMachineSlotStatus.Passive,
            MachineStatus.Switching => MiningMachineSlotStatus.Switching,
            MachineStatus.Sold => MiningMachineSlotStatus.Sold,
            _ => throw new ArgumentOutOfRangeException(nameof(machine), machine.Status, "Unknown machine status")
        };
        StartSwitchingDateTime = machine.StartSwitchingAt;
        EndSwitchingDateTime = machine.EndSwitchingAt;
        TokensAmountCollected = machine.TokensCollected;
        SoldAt = machine.SoldAt;
    }
}