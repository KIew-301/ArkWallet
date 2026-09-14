using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.ShoppingContext.Domain.Machine;

namespace ArkWallet.Core.MiningContext.Domain.Machine;

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
    public long TraderId { get; }
    public MachineType Type { get; }
    public int SwitchingTime { get; }
    public decimal Efficiency { get; }
    public string Image { get; }
    public decimal Cost { get; }
    public DateTime CreatedAt { get; }
    public string? TokenSymbol { get; private set; }
    public long? GlobalRuleId { get; private set; }
    public MachineStatus Status { get; private set; }
    public DateTime? StartSwitchingAt { get; private set; }
    public DateTime? EndSwitchingAt { get; private set; }
    public decimal TokensCollected { get; private set; }
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

    public static Machine Load(
        long traderId,
        MachineType type,
        int switchingTime,
        decimal efficiency,
        string image,
        decimal cost,
        DateTime createdAt,
        string? tokenSymbol,
        long? globalRuleId,
        MachineStatus status,
        DateTime? startSwitchingAt,
        DateTime? endSwitchingAt,
        decimal tokensCollected,
        DateTime? soldAt)
    {
        var machine = new Machine(traderId, type, switchingTime, efficiency, image, cost, createdAt);
        machine.TokenSymbol = tokenSymbol;
        machine.GlobalRuleId = globalRuleId;
        machine.Status = status;
        machine.StartSwitchingAt = startSwitchingAt;
        machine.EndSwitchingAt = endSwitchingAt;
        machine.TokensCollected = tokensCollected;
        machine.SoldAt = soldAt;
        return machine;
    }

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

    public void CompleteSwitching()
    {
        if (Status != MachineStatus.Switching)
            throw new DomainException("Слот не находится в статусе переключения");

        StartSwitchingAt = null;
        EndSwitchingAt = null;
        Status = MachineStatus.Active;
    }

    public void AddTokens(decimal amount) => TokensCollected += amount;

    public int CollectWholeTokens()
    {
        var whole = (int)TokensCollected;
        TokensCollected -= whole;
        return whole;
    }

    public void Sell(TimeProvider? timeProvider = null)
    {
        if (Status == MachineStatus.Sold)
            throw new DomainException("Машина уже продана");

        var soldAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = MachineStatus.Sold;
        SoldAt = soldAt;
    }
}