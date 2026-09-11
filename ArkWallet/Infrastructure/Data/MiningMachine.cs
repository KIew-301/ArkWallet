using ArkWallet.Core.ShoppingContext.Domain.Machine;

namespace ArkWallet.Infrastructure.Data;

/// <summary>
/// Тип майнинг-машины
/// </summary>
public enum MiningMachineType
{
    /// <summary>Автономная одиночная машина</summary>
    SMAI,

    /// <summary>Машина с модульной геометрией</summary>
    MGC,

    /// <summary>Большая производственная машина</summary>
    BMP
}

/// <summary>
/// Майнинг-машина, доступная для покупки.
/// Данные-сущность: содержит только операции создания, обновления и копирования.
/// Имя и стоимость вычисляет доменный агрегат <see cref="Machine"/> (правило 18).
/// </summary>
internal class MiningMachine : EntityData
{
    public long Id { get; }
    public string Name { get; private set; } = string.Empty;
    public MiningMachineType Type { get; private set; }
    public int SwitchingTime { get; private set; }
    public decimal Reusability { get; private set; }
    public bool IsActiveForSale { get; private set; }
    public decimal Cost { get; private set; }
    public decimal Efficiency { get; private set; }
    public string Image { get; private set; } = string.Empty;

    public virtual ICollection<MiningMachineRule> MiningMachineRules { get; set; } = new List<MiningMachineRule>();

    public static MiningMachine Create(
        MiningMachineType type,
        int switchingTime,
        decimal reusability,
        bool isActiveForSale,
        string image,
        decimal efficiency)
    {
        var machine = Machine.Create((MachineType)type, switchingTime, reusability, isActiveForSale, image, efficiency);

        return new MiningMachine
        {
            Type = type,
            SwitchingTime = machine.SwitchingTime,
            Reusability = machine.Reusability,
            IsActiveForSale = machine.IsActiveForSale,
            Image = machine.Image,
            Efficiency = machine.Efficiency,
            Name = machine.Name,
            Cost = machine.Cost
        };
    }

    /// <summary>Обновляет переданные поля и пересобирает имя и стоимость через доменный агрегат</summary>
    public void Update(
        MiningMachineType? type = null,
        int? switchingTime = null,
        decimal? reusability = null,
        bool? isActiveForSale = null,
        string? image = null,
        decimal? efficiency = null)
    {
        var machine = Machine.Load(
            Id,
            (MachineType)Type,
            SwitchingTime,
            Reusability,
            IsActiveForSale,
            Image,
            Efficiency,
            Name,
            Cost);

        machine.Update(
            type.HasValue ? (MachineType)type.Value : null,
            switchingTime,
            reusability,
            isActiveForSale,
            image,
            efficiency,
            MiningMachineRules.Select(r => r.MiningCoefficient).ToArray());

        Type = (MiningMachineType)machine.Type;
        SwitchingTime = machine.SwitchingTime;
        Reusability = machine.Reusability;
        IsActiveForSale = machine.IsActiveForSale;
        Image = machine.Image;
        Efficiency = machine.Efficiency;
        Name = machine.Name;
        Cost = machine.Cost;
    }

    /// <summary>Создаёт независимую копию сущности (новую запись при сохранении)</summary>
    public MiningMachine Copy()
    {
        return new MiningMachine
        {
            Name = Name,
            Type = Type,
            SwitchingTime = SwitchingTime,
            Reusability = Reusability,
            IsActiveForSale = IsActiveForSale,
            Cost = Cost,
            Efficiency = Efficiency,
            Image = Image,
            MiningMachineRules = new List<MiningMachineRule>(MiningMachineRules)
        };
    }
}