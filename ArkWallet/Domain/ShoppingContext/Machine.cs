using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.ShoppingContext;

/// <summary>Тип майнинг-машины в каталоге.</summary>
public enum MachineType
{
    /// <summary>Тип SMAI.</summary>
    SMAI,

    /// <summary>Тип MGC.</summary>
    MGC,

    /// <summary>Тип BMP.</summary>
    BMP
}

internal class Machine
{
    private const decimal MinEfficiency = 0.003m;
    private const decimal MaxEfficiency = 3.910m;
    private const int MinSwitchingTime = 1;
    private const int MaxSwitchingTime = 720;
    private const decimal MinReusability = 40m;
    private const decimal MaxReusability = 95m;

    private static readonly decimal[] CategoryMinEfficiency = [0.003m, 0.006m, 0.011m, 0.018m, 0.033m, 0.046m, 0.084m, 0.157m, 0.373m, 0.763m, 1.476m];
    private static readonly decimal[] CategoryMaxEfficiency = [0.006m, 0.011m, 0.018m, 0.033m, 0.046m, 0.084m, 0.157m, 0.373m, 0.763m, 1.476m, 3.910m];
    private static readonly string[] CategoryNames = ["IRON", "LEAD", "ZINK", "NICKEL", "COPPER", "TIN", "RUTHENIUM", "PLATINUM", "GOLD", "IRIDIUM", "OSMIUM"];
    private static readonly decimal[] CategoryMinCosts = [216m, 864m, 3249m, 9125m, 23789m, 46127m, 102221m, 226089m, 671203m, 1647131m, 3824638m];

    private static readonly string[] SwitchingNames = ["FAST", "MID", "HARD"];
    private static readonly decimal[] SwitchingMarkups = [0.36m, 0.06m, 0m];

    private static readonly string[] ReusabilityLevels = ["E", "D", "C", "B", "A", "S"];
    private static readonly decimal[] ReusabilityMarkups = [0m, 0.02m, 0.04m, 0.07m, 0.09m, 0.12m];

    public long Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MachineType Type { get; private set; }
    public int SwitchingTime { get; private set; }
    public decimal Reusability { get; private set; }
    public bool IsActiveForSale { get; private set; }
    public decimal Cost { get; private set; }
    public decimal Efficiency { get; private set; }
    public string Image { get; private set; } = string.Empty;

    private Machine() { }

    public static Machine Create(
        MachineType type,
        int switchingTime,
        decimal reusability,
        bool isActiveForSale,
        string image,
        decimal efficiency)
    {
        var machine = new Machine
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

    public void Update(
        MachineType? type = null,
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

    public void SetActiveForSale(bool isActiveForSale) => IsActiveForSale = isActiveForSale;

    public decimal GetSellingPrice() => Cost * Reusability / 100m;

    public string DesignName()
    {
        var category = CategoryNames[GetCategoryIndex(Efficiency)];
        var switching = SwitchingNames[GetSwitchingIndex(SwitchingTime)];
        var level = ReusabilityLevels[GetReusabilityIndex(Reusability)];

        return $"{category} {switching} 00-{level}";
    }

    public decimal CalculateCost()
    {
        var categoryIndex = GetCategoryIndex(Efficiency);
        var baseCost = (Efficiency - CategoryMinEfficiency[categoryIndex])
                       / (CategoryMaxEfficiency[categoryIndex] - CategoryMinEfficiency[categoryIndex])
                       * CategoryMinCosts[categoryIndex]
                       + CategoryMinCosts[categoryIndex];

        var switchingMarkup = SwitchingMarkups[GetSwitchingIndex(SwitchingTime)];
        var reusabilityMarkup = ReusabilityMarkups[GetReusabilityIndex(Reusability)];

        var total = baseCost * (1m + switchingMarkup + reusabilityMarkup);
        return Math.Round(total);
    }

    public void RecomputeNameAndCost()
    {
        Name = DesignName();
        Cost = CalculateCost();
    }

    private void SetSwitchingTime(int switchingTime)
    {
        if (switchingTime < MinSwitchingTime || switchingTime > MaxSwitchingTime)
            throw new DomainException($"Switching time must be between {MinSwitchingTime} and {MaxSwitchingTime} minutes");
        SwitchingTime = switchingTime;
    }

    private void SetReusability(decimal reusability)
    {
        if (reusability < MinReusability || reusability > MaxReusability)
            throw new DomainException($"Reusability must be between {MinReusability}% and {MaxReusability}%");
        Reusability = reusability;
    }

    private void SetEfficiency(decimal efficiency)
    {
        if (efficiency < MinEfficiency || efficiency > MaxEfficiency)
            throw new DomainException($"Efficiency must be between {MinEfficiency} and {MaxEfficiency}");
        Efficiency = efficiency;
    }

    private void SetImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new DomainException("Image cannot be empty");
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
}
