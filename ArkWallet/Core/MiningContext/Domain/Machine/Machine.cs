using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.ShoppingContext.Domain.Machine;

namespace ArkWallet.Core.MiningContext.Domain.Machine;

/// <summary>Команда восстановления машины майнинга из хранилища.</summary>
/// <param name="TraderId">Идентификатор трейдера.</param>
/// <param name="Type">Тип майнинг-машины.</param>
/// <param name="SwitchingTime">Время переключения в секундах.</param>
/// <param name="Efficiency">Эффективность машины.</param>
/// <param name="Image">URL изображения машины.</param>
/// <param name="Cost">Стоимость машины.</param>
/// <param name="CreatedAt">Дата создания записи.</param>
/// <param name="TokenSymbol">Символ токена.</param>
/// <param name="GlobalRuleId">ID глобального правила.</param>
/// <param name="Status">Статус машины.</param>
/// <param name="StartSwitchingAt">Дата начала переключения.</param>
/// <param name="EndSwitchingAt">Дата окончания переключения.</param>
/// <param name="TokensCollected">Количество собранных токенов.</param>
/// <param name="SoldAt">Дата продажи.</param>
public record MachineLoadCommand(
    long TraderId,
    ArkWallet.Core.ShoppingContext.Domain.Machine.MachineType Type,
    int SwitchingTime,
    decimal Efficiency,
    string Image,
    decimal Cost,
    DateTime CreatedAt,
    string? TokenSymbol,
    long? GlobalRuleId,
    MachineStatus Status,
    DateTime? StartSwitchingAt,
    DateTime? EndSwitchingAt,
    decimal TokensCollected,
    DateTime? SoldAt);

/// <summary>Статус майнинг-машины в слоте трейдера.</summary>
public enum MachineStatus
{
    /// <summary>Машина активна и добывает токены.</summary>
    Active,

    /// <summary>Машина пассивна и не добывает.</summary>
    Passive,

    /// <summary>Машина переключается на новую цель.</summary>
    Switching,

    /// <summary>Машина продана.</summary>
    Sold
}

/// <summary>
/// Агрегат майнинг-машины в слоте трейдера. Владеет бизнес-логикой переключения,
/// накопления, сбора и продажи. Data-сущность (EF) <c>MiningMachineSlot</c> делегирует
/// изменения сюда и не содержит бизнес-логики (правило 18).
/// </summary>
public class Machine
{
    /// <summary>Идентификатор трейдера-владельца.</summary>
    public long TraderId { get; }
    /// <summary>Тип каталогной машины (характеристики скопированы при покупке).</summary>
    public MachineType Type { get; }
    /// <summary>Время переключения машины в минутах.</summary>
    public int SwitchingTime { get; }
    /// <summary>Эффективность добычи.</summary>
    public decimal Efficiency { get; }
    /// <summary>Строковый идентификатор изображения машины.</summary>
    public string Image { get; }
    /// <summary>Цена продажи машины.</summary>
    public decimal Cost { get; }
    /// <summary>Момент создания слота.</summary>
    public DateTime CreatedAt { get; }
    /// <summary>Текущий токен добычи (null пока машина не переключена).</summary>
    public string? TokenSymbol { get; private set; }
    /// <summary>Активное глобальное правило для текущего токена.</summary>
    public long? GlobalRuleId { get; private set; }
    /// <summary>Текущий статус машины.</summary>
    public MachineStatus Status { get; private set; }
    /// <summary>Начало переключения (null если не переключается).</summary>
    public DateTime? StartSwitchingAt { get; private set; }
    /// <summary>Окончание переключения.</summary>
    public DateTime? EndSwitchingAt { get; private set; }
    /// <summary>Накопленные, ещё не собранные токены.</summary>
    public decimal TokensCollected { get; private set; }
    /// <summary>Момент продажи (null если не продана).</summary>
    public DateTime? SoldAt { get; private set; }

    private Machine(
        long traderId,
        MachineType type,
        int switchingTime,
        decimal efficiency,
        string image,
        decimal cost,
        DateTime createdAt)
    {
        TraderId = traderId;
        Type = type;
        SwitchingTime = switchingTime;
        Efficiency = efficiency;
        Image = image;
        Cost = cost;
        Status = MachineStatus.Passive;
        CreatedAt = createdAt;
    }

    /// <summary>Создаёт новую машину в статусе Passive с валидацией входных параметров.</summary>
    public static Machine Purchase(
        long traderId,
        MachineType type,
        int switchingTime,
        decimal efficiency,
        string image,
        decimal cost,
        TimeProvider? timeProvider = null)
    {
        if (switchingTime <= 0)
            throw new DomainException("Switching time must be greater than 0");
        if (efficiency <= 0)
            throw new DomainException("Efficiency must be greater than 0");
        if (string.IsNullOrWhiteSpace(image))
            throw new DomainException("Image cannot be empty");
        if (cost <= 0)
            throw new DomainException("Cost must be greater than 0");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new Machine(traderId, type, switchingTime, efficiency, image, cost, createdAt);
    }

    /// <summary>Восстанавливает машину из постоянного хранилища без валидации.</summary>
    public static Machine Load(MachineLoadCommand data)
    {
        var machine = new Machine(data.TraderId, data.Type, data.SwitchingTime, data.Efficiency, data.Image, data.Cost, data.CreatedAt);
        machine.TokenSymbol = data.TokenSymbol;
        machine.GlobalRuleId = data.GlobalRuleId;
        machine.Status = data.Status;
        machine.StartSwitchingAt = data.StartSwitchingAt;
        machine.EndSwitchingAt = data.EndSwitchingAt;
        machine.TokensCollected = data.TokensCollected;
        machine.SoldAt = data.SoldAt;
        return machine;
    }

    /// <summary>Запускает переключение машины в режим майнинга.</summary>
    /// <param name="traderId">Идентификатор трейдера.</param>
    /// <param name="tokenSymbol">Символ токена для подсчёта добычи.</param>
    /// <param name="globalRuleId">ID глобального правила.</param>
    /// <param name="timeProvider">Поставщик времени (по умолчанию — SystemTime.Now).</param>
    public void StartSwitching(
        long traderId,
        string tokenSymbol,
        long globalRuleId,
        TimeProvider? timeProvider = null)
    {
        if (TraderId != traderId)
            throw new DomainException("Трейдер не владеет данной машиной");
        if (Status == MachineStatus.Sold)
            throw new DomainException("Машина уже продана");
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Символ токена не может быть пустым");

        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        TokenSymbol = tokenSymbol;
        GlobalRuleId = globalRuleId;
        StartSwitchingAt = utcNow;
        EndSwitchingAt = utcNow.AddMinutes(SwitchingTime);
        Status = MachineStatus.Switching;
    }

    /// <summary>Завершает переключение и переводит машину в Active.</summary>
    public void CompleteSwitching()
    {
        if (Status != MachineStatus.Switching)
            throw new DomainException("Слот не находится в статусе переключения");

        StartSwitchingAt = null;
        EndSwitchingAt = null;
        Status = MachineStatus.Active;
    }

    /// <summary>Добавляет накопленные токены.</summary>
    public void AddTokens(decimal amount) => TokensCollected += amount;

    /// <summary>Возвращает целую часть накопленных токенов и вычитает её из накопления.</summary>
    public int CollectWholeTokens()
    {
        var whole = (int)TokensCollected;
        TokensCollected -= whole;
        return whole;
    }

    /// <summary>Продаёт машину и фиксирует SoldAt.</summary>
    public void Sell(TimeProvider? timeProvider = null)
    {
        if (Status == MachineStatus.Sold)
            throw new DomainException("Машина уже продана");

        var soldAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = MachineStatus.Sold;
        SoldAt = soldAt;
    }
}