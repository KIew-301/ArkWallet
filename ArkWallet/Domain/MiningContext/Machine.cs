using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ShoppingContext;

namespace ArkWallet.Domain.MiningContext;

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

internal class Machine
{
    public long Id { get; private set; }
    public long TraderId { get; }
    public long MachineCatalogId { get; }
    public MachineType Type { get; }
    public int SwitchingTime { get; }
    public decimal Efficiency { get; }
    public string Image { get; }
    public string? TokenSymbol { get; private set; }
    public long? GlobalRuleId { get; private set; }
    public MachineStatus Status { get; private set; }
    public DateTime? StartSwitchingAt { get; private set; }
    public DateTime? EndSwitchingAt { get; private set; }
    public decimal TokensCollected { get; private set; }
    public decimal Cost { get; }
    public DateTime CreatedAt { get; }
    public DateTime? SoldAt { get; private set; }

    private Machine(
        long traderId,
        long machineCatalogId,
        MachineType type,
        int switchingTime,
        decimal efficiency,
        string image,
        decimal cost,
        DateTime createdAt)
    {
        TraderId = traderId;
        MachineCatalogId = machineCatalogId;
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
        long machineCatalogId,
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
        return new Machine(traderId, machineCatalogId, type, switchingTime, efficiency, image, cost, createdAt);
    }

    public void StartSwitching(string tokenSymbol, long globalRuleId, TimeProvider? timeProvider = null)
    {
        if (Status == MachineStatus.Sold)
            throw new DomainException("Cannot switch a sold machine");
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");

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
            throw new DomainException("Machine is not switching");

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
            throw new DomainException("Machine is already sold");

        var soldAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = MachineStatus.Sold;
        SoldAt = soldAt;
    }
}
