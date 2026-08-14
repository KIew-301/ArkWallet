using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.Entities;

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
/// Майнинг-машина, доступная для покупки
/// </summary>
internal class MiningMachine
{
    public long Id { get; private set; }
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
        string name,
        MiningMachineType type,
        int switchingTime,
        decimal reusability,
        bool isActiveForSale,
        decimal cost,
        string image,
        decimal efficiency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название машины не может быть пустым");
        if (switchingTime <= 0)
            throw new DomainException("Время переключения должно быть больше нуля");
        if (reusability < 0 || reusability > 100)
            throw new DomainException("Процент возврата должен быть от 0 до 100");
        if (cost <= 0)
            throw new DomainException("Цена машины должна быть больше нуля");
        if (string.IsNullOrWhiteSpace(image))
            throw new DomainException("Ссылка на изображение не может быть пустой");
        if (efficiency <= 0)
            throw new DomainException("Коэффициент производительности должен быть больше нуля");

        return new MiningMachine
        {
            Name = name,
            Type = type,
            SwitchingTime = switchingTime,
            Reusability = reusability,
            IsActiveForSale = isActiveForSale,
            Cost = cost,
            Efficiency = efficiency,
            Image = image
        };
    }

    /// <summary>Цена продажи машины владельцем: стоимость покупки * процент возврата</summary>
    public decimal GetSellingPrice()
        => Cost * Reusability / 100m;

    public void SetActiveForSale(bool isActiveForSale)
        => IsActiveForSale = isActiveForSale;
}
