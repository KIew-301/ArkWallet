using ArkWallet.Core.General.Domain.Exceptions;

namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Роль бота на рынке: покупатель, продавец или стена.</summary>
public enum MarketMakerRole
{
    /// <summary>Бот действует в роли покупателя.</summary>
    Buyer,

    /// <summary>Бот действует в роли продавца.</summary>
    Seller,

    /// <summary>Бот-«стена» (WallBlocker).</summary>
    Waller
}

public sealed class MarketMakerBot
{
    /// <summary>Баланс трейдера по умолчанию для бота.</summary>
    public const decimal DefaultBalance = 10_000_000m;

    /// <summary>Портфель (токены символа) по умолчанию для бота.</summary>
    public const int DefaultPortfolioTokens = 1_000_000;

    public long Id { get; private set; }
    public long TraderId { get; }
    public string Symbol { get; }
    public decimal BasePower { get; private set; }
    public decimal ActivePower { get; private set; }
    public MarketMakerRole Role { get; private set; }
    public decimal PowerDeviationCoeff { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; }

    private MarketMakerBot(long traderId, string symbol, MarketMakerRole role, decimal basePower, DateTime createdAt)
    {
        TraderId = traderId;
        Symbol = symbol;
        Role = role;
        BasePower = basePower;
        ActivePower = basePower;
        PowerDeviationCoeff = 1m;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static MarketMakerBot Create(long traderId, string symbol, MarketMakerRole role, decimal initialPower = 50m, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Symbol cannot be empty");
        if (initialPower <= 0)
            throw new DomainException("Initial power must be greater than 0");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new MarketMakerBot(traderId, symbol, role, initialPower, createdAt);
    }

    internal static MarketMakerBot Load(long id, long traderId, string symbol, MarketMakerRole role, decimal basePower, bool isActive, DateTime createdAt, decimal powerDeviationCoeff = 1)
    {
        var marketMakerBot = new MarketMakerBot(traderId, symbol, role, basePower, createdAt)
        {
            Id = id,
            IsActive = isActive,
            PowerDeviationCoeff = powerDeviationCoeff
        };
        return marketMakerBot;
    }

    public void UpdatePower(decimal minPower, decimal maxPower)
    {
        if (minPower >= maxPower)
            throw new DomainException("Min power must be less than max power");

        var change = Random.Shared.Next(-35, 35);
        BasePower = Math.Clamp(BasePower + change, minPower, maxPower);
    }

    public void UpdatePowerDeviationCoeff(decimal newCoeff) => PowerDeviationCoeff = Math.Clamp(newCoeff, 1m, 5.5m);

    public void SetRole(MarketMakerRole role) => Role = role;

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>
    /// Выполняет план: прогоняет коллекцию модификаторов по конвейеру.
    /// Каждый модификатор получает текущий план и возвращает обновлённый (источники добавляют ордера, трансформаторы меняют quantity).
    /// </summary>
    /// <summary>Перегрузка без рыночных условий (используется до внедрения модификаторов с контекстом).</summary>
    internal IReadOnlyCollection<CreateMarketOrderCommand> ExecutePlan(IReadOnlyCollection<IPlanModify> modifiers)
        => ExecutePlan(modifiers, new MarketConditions(0m, null, null, Array.Empty<PlacedOrderLevel>()));

    internal IReadOnlyCollection<CreateMarketOrderCommand> ExecutePlan(
        IReadOnlyCollection<IPlanModify> modifiers, MarketConditions market)
    {
        var plan = new List<CreateMarketOrderCommand>();
        foreach (var modify in modifiers)
            plan = modify.Build(this, market, plan).ToList();
        return plan;
    }

    /// <summary>
    /// Пересчитывает активную мощность: прогоняет коллекцию модификаторов, каждый из которых меняет ActivePower.
    /// </summary>
    public void RebalancePower(IReadOnlyCollection<IPowerCalculationModify> modifiers)
    {
        foreach (var modify in modifiers)
            modify.Apply(this);
    }

    /// <summary>Задает активную мощность (внутренний сеттер для модификаторов мощности).</summary>
    internal void SetActivePower(decimal value) => ActivePower = value;

    /// <summary>Возвращает дефолтные ресурсы бота: баланс трейдера и размер портфеля токенов.</summary>
    public (decimal Balance, int PortfolioTokens) GetDefaultResources() => (DefaultBalance, DefaultPortfolioTokens);
}
