using ArkWallet.Domain.Engines;
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
/// Майнинг-машина, доступная для покупки.
/// Имя и стоимость не задаются извне, а вычисляются методами DesignName() и CalculateCost().
/// </summary>
internal class MiningMachine
{
    private const decimal MinEfficiency = 0.003m;
    private const decimal MaxEfficiency = 3.910m;
    private const int MinSwitchingTime = 1;
    private const int MaxSwitchingTime = 720;
    private const decimal MinReusability = 40m;
    private const decimal MaxReusability = 95m;

    // Категории производительности. Диапазоны (min, max], первая включает абсолютный минимум 0.003.
    private static readonly decimal[] CategoryMinEfficiency = [0.003m, 0.006m, 0.011m, 0.018m, 0.033m, 0.046m, 0.084m, 0.157m, 0.373m, 0.763m, 1.476m];
    private static readonly decimal[] CategoryMaxEfficiency = [0.006m, 0.011m, 0.018m, 0.033m, 0.046m, 0.084m, 0.157m, 0.373m, 0.763m, 1.476m, 3.910m];
    private static readonly string[] CategoryNames = ["IRON", "LEAD", "ZINK", "NICKEL", "COPPER", "TIN", "RUTHENIUM", "PLATINUM", "GOLD", "IRIDIUM", "OSMIUM"];
    private static readonly decimal[] CategoryMinCosts = [216m, 864m, 3249m, 9125m, 23789m, 46127m, 102221m, 226089m, 671203m, 1647131m, 3824638m];

    // Время переключения. Диапазоны: FAST [1, 20), MID [20, 120), HARD [120, 720].
    private static readonly string[] SwitchingNames = ["FAST", "MID", "HARD"];
    private static readonly decimal[] SwitchingMarkups = [0.36m, 0.06m, 0m];

    // Переиспользуемость. Диапазоны: E [40, 50), D [50, 60), C [60, 70), B [70, 80), A [80, 90), S [90, 95].
    private static readonly string[] ReusabilityLevels = ["E", "D", "C", "B", "A", "S"];
    private static readonly decimal[] ReusabilityMarkups = [0m, 0.02m, 0.04m, 0.07m, 0.09m, 0.12m];

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
        MiningMachineType type,
        int switchingTime,
        decimal reusability,
        bool isActiveForSale,
        string image,
        decimal efficiency)
    {
        var machine = new MiningMachine
        {
            Type = type,
            IsActiveForSale = isActiveForSale
        };
        machine.SetSwitchingTime(switchingTime);
        machine.SetReusability(reusability);
        machine.SetImage(image);
        machine.SetEfficiency(efficiency);
        machine.RecomputeNameAndCost();
        return machine;
    }

    /// <summary>Обновляет переданные поля машины и пересобирает имя и стоимость</summary>
    public void Update(
        MiningMachineType? type = null,
        int? switchingTime = null,
        decimal? reusability = null,
        bool? isActiveForSale = null,
        string? image = null,
        decimal? efficiency = null)
    {
        if (type.HasValue)
            Type = type.Value;
        if (switchingTime.HasValue)
            SetSwitchingTime(switchingTime.Value);
        if (reusability.HasValue)
            SetReusability(reusability.Value);
        if (isActiveForSale.HasValue)
            IsActiveForSale = isActiveForSale.Value;
        if (image != null)
            SetImage(image);
        if (efficiency.HasValue)
            SetEfficiency(efficiency.Value);

        RecomputeNameAndCost();
    }

    /// <summary>
    /// Собирает имя машины по формату [категория] [время переключения] [эффективные][стабильные токены]-[уровень].
    /// Без правил числовая часть равна "00".
    /// </summary>
    public string DesignName()
    {
        var category = CategoryNames[GetCategoryIndex(Efficiency)];
        var switching = SwitchingNames[GetSwitchingIndex(SwitchingTime)];
        var (effective, stable) = CountTokenAdaptivity();
        var level = ReusabilityLevels[GetReusabilityIndex(Reusability)];

        return $"{category} {switching} {effective}{stable}-{level}";
    }

    /// <summary>
    /// Считает стоимость машины: базовая стоимость категории плюс наценки
    /// за время переключения, уровень переиспользуемости и адаптивность под токены.
    /// Округляется до целого.
    /// </summary>
    public decimal CalculateCost()
    {
        var categoryIndex = GetCategoryIndex(Efficiency);
        var baseCost = (Efficiency - CategoryMinEfficiency[categoryIndex])
                       / (CategoryMaxEfficiency[categoryIndex] - CategoryMinEfficiency[categoryIndex])
                       * CategoryMinCosts[categoryIndex]
                       + CategoryMinCosts[categoryIndex];

        var switchingMarkup = SwitchingMarkups[GetSwitchingIndex(SwitchingTime)];
        var reusabilityMarkup = ReusabilityMarkups[GetReusabilityIndex(Reusability)];
        var (effective, stable) = CountTokenAdaptivity();
        var tokensMarkup = effective * 0.05m + stable * 0.02m;

        var total = baseCost * (1m + switchingMarkup + reusabilityMarkup + tokensMarkup);
        return Math.Round(total);
    }

    /// <summary>Пересобирает имя и пересчитывает стоимость по текущим параметрам и правилам</summary>
    public void RecomputeNameAndCost()
    {
        Name = DesignName();
        Cost = CalculateCost();
    }

    /// <summary>Цена продажи машины владельцем: стоимость покупки * процент возврата</summary>
    public decimal GetSellingPrice()
        => Cost * Reusability / 100m;

    public void SetActiveForSale(bool isActiveForSale)
        => IsActiveForSale = isActiveForSale;

    private void SetSwitchingTime(int switchingTime)
    {
        if (switchingTime < MinSwitchingTime || switchingTime > MaxSwitchingTime)
            throw new DomainException($"Время переключения должно быть от {MinSwitchingTime} до {MaxSwitchingTime} минут");
        SwitchingTime = switchingTime;
    }

    private void SetReusability(decimal reusability)
    {
        if (reusability < MinReusability || reusability > MaxReusability)
            throw new DomainException($"Переиспользуемость должна быть от {MinReusability}% до {MaxReusability}%");
        Reusability = reusability;
    }

    private void SetEfficiency(decimal efficiency)
    {
        if (efficiency < MinEfficiency || efficiency > MaxEfficiency)
            throw new DomainException($"Коэффициент производительности должен быть от {MinEfficiency} до {MaxEfficiency}");
        Efficiency = efficiency;
    }

    private void SetImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new DomainException("Ссылка на изображение не может быть пустой");
        Image = image;
    }

    private static int GetCategoryIndex(decimal efficiency)
    {
        for (var i = 0; i < CategoryMaxEfficiency.Length; i++)
            if (efficiency < CategoryMaxEfficiency[i])
                return i;
        return CategoryMaxEfficiency.Length - 1;
    }

    private static int GetSwitchingIndex(int switchingTime)
    {
        if (switchingTime < 20)
            return 0;
        if (switchingTime < 120)
            return 1;
        return 2;
    }

    private static int GetReusabilityIndex(decimal reusability)
    {
        if (reusability < 50)
            return 0;
        if (reusability < 60)
            return 1;
        if (reusability < 70)
            return 2;
        if (reusability < 80)
            return 3;
        if (reusability < 90)
            return 4;
        return 5;
    }

    private (int Effective, int Stable) CountTokenAdaptivity()
    {
        var effective = 0;
        var stable = 0;

        foreach (var rule in MiningMachineRules)
        {
            if (rule.MiningCoefficient >= MiningEngine.EffectiveMiningCoefficientMin)
                effective++;
            else
                stable++;
        }

        return (effective, stable);
    }
}
