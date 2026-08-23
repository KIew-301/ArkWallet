namespace ArkWallet.Domain.Entities;

/// <summary>
/// Правило майнинга слота: копия правила каталогной машины,
/// зафиксированная на момент покупки.
/// </summary>
internal class MiningMachineSlotRule
{
    public long Id { get; }
    public long MiningMachineSlotId { get; }
    public string CharacterTokenId { get; private set; } = string.Empty;
    public decimal MiningCoefficient { get; private set; }

    public virtual MiningMachineSlot? MiningMachineSlot { get; set; }
    public virtual CharacterToken? CharacterToken { get; set; }

    /// <summary>Создаёт правило слота</summary>
    public static MiningMachineSlotRule Create(string characterTokenId, decimal miningCoefficient)
    {
        return new MiningMachineSlotRule
        {
            CharacterTokenId = characterTokenId,
            MiningCoefficient = miningCoefficient
        };
    }

    /// <summary>Копирует правило каталогной машины в слот</summary>
    public static MiningMachineSlotRule Copy(MiningMachineRule rule)
        => Create(rule.CharacterTokenId, rule.MiningCoefficient);
}
