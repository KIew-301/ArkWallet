using ArkWallet.Core.General.Domain.Exceptions;

namespace ArkWallet.Core.ShoppingContext.Domain.Machine;

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

/// <summary>Команда восстановления машины из каталога покупок.</summary>
public record ShoppingMachineLoadCommand(
    /// <summary>Идентификатор машины.</summary>
    long Id,
    /// <summary>Тип майнинг-машины.</summary>
    MachineType Type,
    /// <summary>Время переключения в секундах.</summary>
    int SwitchingTime,
    /// <summary>Повторная используемость (ресурс).</summary>
    decimal Reusability,
    /// <summary>Активна ли для продажи.</summary>
    bool IsActiveForSale,
    /// <summary>URL изображения машины.</summary>
    string Image,
    /// <summary>Эффективность машины.</summary>
    decimal Efficiency,
    /// <summary>Название машины.</summary>
    string Name,
    /// <summary>Стоимость машины.</summary>
    decimal Cost);

/// <summary>
/// Агрегат майнинг-машины в каталоге. Владеет бизнес-логикой валидации,
/// формирования имени, расчёта стоимости и цены продажи. Data-сущности (EF)
/// делегируют вычисления сюда и не содержат бизнес-логики (правило 18).
/// </summary>
public class Machine
{
    private const decimal MinEfficiency = 0.003m;
    private const decimal MaxEfficiency = 3.910m;
    private const int MinSwitchingTime = 1;
    private const int MaxSwitchingTime = 720;
    private const decimal MinReusability = 40m;
    private const decimal MaxReusability = 95m;

    /// <summary>Порог: правило с коэффициентом не ниже порога — «эффективное», иначе «стабильное».</summary>
    private const decimal EffectiveMiningCoefficientMin = 0.85m;

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

    private Machine() { }

    /// <summary>Идентификатор машины в каталоге.</summary>
    public long Id { get; private set; }
    /// <summary>Сформированное имя машины.</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>Тип машины.</summary>
    public MachineType Type { get; private set; }
    /// <summary>Время переключения в минутах.</summary>
    public int SwitchingTime { get; private set; }
    /// <summary>Процент возврата при продаже.</summary>
    public decimal Reusability { get; private set; }
    /// <summary>Доступна ли машина для продажи в каталоге.</summary>
    public bool IsActiveForSale { get; private set; }
    /// <summary>Стоимость покупки.</summary>
    public decimal Cost { get; private set; }
    /// <summary>Эффективность добычи.</summary>
    public decimal Efficiency { get; private set; }
    /// <summary>Ссылка на изображение машины.</summary>
    public string Image { get; private set; } = string.Empty;

    /// <summary>Создаёт машину в каталоге с валидацией параметров.</summary>
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
        machine.ValidateAndSetSwitchingTime(switchingTime);
        machine.ValidateAndSetReusability(reusability);
        machine.ValidateAndSetImage(image);
        machine.ValidateAndSetEfficiency(efficiency);
        machine.RecomputeNameAndCost(Array.Empty<decimal>());
        return machine;
    }

    /// <summary>Восстанавливает агрегат из данных сущности без повторной валидации.</summary>
    public static Machine Load(ShoppingMachineLoadCommand data)
    {
        return new Machine
        {
            Id = data.Id,
            Type = data.Type,
            SwitchingTime = data.SwitchingTime,
            Reusability = data.Reusability,
            IsActiveForSale = data.IsActiveForSale,
            Image = data.Image,
            Efficiency = data.Efficiency,
            Name = data.Name,
            Cost = data.Cost
        };
    }

    /// <summary>Обновляет переданные поля и пересобирает имя и стоимость.</summary>
    public void Update(
        MachineType? type = null,
        int? switchingTime = null,
        decimal? reusability = null,
        bool? isActiveForSale = null,
        string? image = null,
        decimal? efficiency = null,
        IReadOnlyCollection<decimal>? miningCoefficients = null)
    {
        if (type.HasValue)
            Type = type.Value;
        if (switchingTime.HasValue)
            ValidateAndSetSwitchingTime(switchingTime.Value);
        if (reusability.HasValue)
            ValidateAndSetReusability(reusability.Value);
        if (isActiveForSale.HasValue)
            IsActiveForSale = isActiveForSale.Value;
        if (image != null)
            ValidateAndSetImage(image);
        if (efficiency.HasValue)
            ValidateAndSetEfficiency(efficiency.Value);

        RecomputeNameAndCost(miningCoefficients ?? Array.Empty<decimal>());
    }

    /// <summary>Цена продажи машины владельцем: стоимость покупки * процент возврата.</summary>
    public decimal GetSellingPrice()
        => Cost * Reusability / 100m;

    /// <summary>Цена продажи машины владельцем (без полной реконструкции агрегата).</summary>
    public static decimal CalculateSellingPrice(decimal cost, decimal reusability)
        => cost * reusability / 100m;

    /// <summary>
    /// Собирает имя машины по формату [категория] [время переключения] [эффективные][стабильные токены]-[уровень].
    /// Без правил числовая часть равна "00".
    /// </summary>
    public (string Name, decimal Cost) RecomputeNameAndCost(IReadOnlyCollection<decimal> miningCoefficients)
    {
        Name = DesignName(miningCoefficients);
        Cost = CalculateCost(miningCoefficients);
        return (Name, Cost);
    }

    private string DesignName(IReadOnlyCollection<decimal> miningCoefficients)
    {
        var category = CategoryNames[GetCategoryIndex(Efficiency)];
        var switching = SwitchingNames[GetSwitchingIndex(SwitchingTime)];
        var (effective, stable) = CountTokenAdaptivity(miningCoefficients);
        var level = ReusabilityLevels[GetReusabilityIndex(Reusability)];

        return $"{category} {switching} {effective}{stable}-{level}";
    }

    private decimal CalculateCost(IReadOnlyCollection<decimal> miningCoefficients)
    {
        var categoryIndex = GetCategoryIndex(Efficiency);
        var baseCost = (Efficiency - CategoryMinEfficiency[categoryIndex])
                       / (CategoryMaxEfficiency[categoryIndex] - CategoryMinEfficiency[categoryIndex])
                       * CategoryMinCosts[categoryIndex]
                       + CategoryMinCosts[categoryIndex];

        var switchingMarkup = SwitchingMarkups[GetSwitchingIndex(SwitchingTime)];
        var reusabilityMarkup = ReusabilityMarkups[GetReusabilityIndex(Reusability)];
        var (effective, stable) = CountTokenAdaptivity(miningCoefficients);
        var tokensMarkup = effective * 0.05m + stable * 0.02m;

        var total = baseCost * (1m + switchingMarkup + reusabilityMarkup + tokensMarkup);
        return Math.Round(total);
    }

    private void ValidateAndSetSwitchingTime(int switchingTime)
    {
        if (switchingTime < MinSwitchingTime || switchingTime > MaxSwitchingTime)
            throw new DomainException($"Время переключения должно быть от {MinSwitchingTime} до {MaxSwitchingTime} минут");
        SwitchingTime = switchingTime;
    }

    private void ValidateAndSetReusability(decimal reusability)
    {
        if (reusability < MinReusability || reusability > MaxReusability)
            throw new DomainException($"Переиспользуемость должна быть от {MinReusability}% до {MaxReusability}%");
        Reusability = reusability;
    }

    private void ValidateAndSetEfficiency(decimal efficiency)
    {
        if (efficiency < MinEfficiency || efficiency > MaxEfficiency)
            throw new DomainException($"Коэффициент производительности должен быть от {MinEfficiency} до {MaxEfficiency}");
        Efficiency = efficiency;
    }

    private void ValidateAndSetImage(string image)
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

    private static (int Effective, int Stable) CountTokenAdaptivity(IReadOnlyCollection<decimal> miningCoefficients)
    {
        var effective = 0;
        var stable = 0;

        foreach (var coefficient in miningCoefficients)
        {
            if (coefficient >= EffectiveMiningCoefficientMin)
                effective++;
            else
                stable++;
        }

        return (effective, stable);
    }
}