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

/// <summary>Represents a market maker bot with configurable power, role, and trading parameters.</summary>
public sealed class MarketMakerBot
{
    /// <summary>The default trader balance for the bot.</summary>
    public const decimal DefaultBalance = 10_000_000m;

    /// <summary>Портфель (токены символа) по умолчанию для бота.</summary>
    public const int DefaultPortfolioTokens = 1_000_000;

    /// <summary>Unique identifier of the bot.</summary>
    public long Id { get; private set; }
    /// <summary>ID of the trader this bot belongs to.</summary>
    public long TraderId { get; }
    /// <summary>The trading pair symbol (e.g., BTC/USDT).</summary>
    public string Symbol { get; }
    /// <summary>The base power value used for calculations.</summary>
    public decimal BasePower { get; private set; }
    /// <summary>The currently active power after applying modifiers.</summary>
    public decimal ActivePower { get; private set; }
    /// <summary>The role this bot plays in the market.</summary>
    public MarketMakerRole Role { get; private set; }
    /// <summary>Coefficient for power deviation calculations.</summary>
    public decimal PowerDeviationCoeff { get; private set; }
    /// <summary>Whether this bot is currently active.</summary>
    public bool IsActive { get; private set; }
    /// <summary>The UTC timestamp when this bot was created.</summary>
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

    /// <summary>Creates a new market maker bot instance.</summary>
    /// <param name="traderId">The ID of the trader this bot belongs to.</param>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="role">The market making role.</param>
    /// <param name="initialPower">The initial base power (default: 50).</param>
    /// <param name="timeProvider">Optional time provider for deterministic testing.</param>
    /// <returns>A new <see cref="MarketMakerBot"/> instance.</returns>
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

    /// <summary>Updates the base power with a random deviation clamped to the specified range.</summary>
    /// <param name="minPower">The minimum allowed power value.</param>
    /// <param name="maxPower">The maximum allowed power value.</param>
    public void UpdatePower(decimal minPower, decimal maxPower)
    {
        if (minPower >= maxPower)
            throw new DomainException("Min power must be less than max power");

        var change = Random.Shared.Next(-35, 35);
        BasePower = Math.Clamp(BasePower + change, minPower, maxPower);
    }

    /// <summary>Updates the power deviation coefficient, clamping it to [1, 5.5].</summary>
    /// <param name="newCoeff">The new deviation coefficient.</param>
    public void UpdatePowerDeviationCoeff(decimal newCoeff) => PowerDeviationCoeff = Math.Clamp(newCoeff, 1m, 5.5m);

    /// <summary>Sets the trading role of this bot.</summary>
    /// <param name="role">The new market maker role.</param>
    public void SetRole(MarketMakerRole role) => Role = role;

    /// <summary>Sets the active status of this bot.</summary>
    /// <param name="isActive">Whether the bot is active.</param>
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
    public static (decimal Balance, int PortfolioTokens) GetDefaultResources() => (DefaultBalance, DefaultPortfolioTokens);
}
