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
    /// <summary>Баланс трейдера по умолчанию для бота — 1 000 000 000.</summary>
    public const decimal DefaultBalance = 1_000_000_000m;

    /// <summary>Портфель (токены символа) по умолчанию для бота — 1 000 000.</summary>
    public const int DefaultPortfolioTokens = 1_000_000;

    /// <summary>Уникальный идентификатор бота.</summary>
    public long Id { get; private set; }
    /// <summary>Идентификатор трейдера, которому принадлежит этот бот.</summary>
    public long TraderId { get; private set; }
    /// <summary>Торговая пара символа (например, BTC/USDT).</summary>
    public string Symbol { get; }
    /// <summary>Базовое значение мощности, используемое для расчётов.</summary>
    public decimal BasePower { get; private set; }
    /// <summary>Текущая активная мощность после применения модификаторов.</summary>
    public decimal ActivePower { get; private set; }
    /// <summary>Роль, которую бот выполняет на рынке.</summary>
    public MarketMakerRole Role { get; private set; }
    /// <summary>Активен ли этот бот в настоящее время.</summary>
    public bool IsActive { get; private set; }
    /// <summary>Метка времени создания бота (UTC).</summary>
    public DateTime CreatedAt { get; }

    private MarketMakerBot(long traderId, string symbol, MarketMakerRole role, decimal basePower, DateTime createdAt)
    {
        TraderId = traderId;
        Symbol = symbol;
        Role = role;
        BasePower = basePower;
        ActivePower = basePower;
        IsActive = true;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Создаёт новый экземпляр бота-маркетмейкера.
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера, которому принадлежит бот.</param>
    /// <param name="symbol">Торговый символ пары (например, BTC/USDT).</param>
    /// <param name="role">Роль маркетмейкера на рынке.</param>
    /// <param name="initialPower">Начальная базовая мощность (по умолчанию 50).</param>
    /// <param name="timeProvider">Поставщик времени для детерминированного тестирования.</param>
    /// <returns>Новый экземпляр <see cref="MarketMakerBot"/>.</returns>
    public static MarketMakerBot Create(long traderId, string symbol, MarketMakerRole role, decimal initialPower = 50m, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Symbol cannot be empty");
        if (initialPower <= 0)
            throw new DomainException("Initial power must be greater than 0");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new MarketMakerBot(traderId, symbol, role, initialPower, createdAt);
    }

    /// <summary>
    /// Восстанавливает бота из сохранённых данных (загрузка из БД).
    /// </summary>
    /// <param name="data">Снимок сохранённых параметров бота.</param>
    internal static MarketMakerBot Load(MarketMakerBotLoadData data)
    {
        var marketMakerBot = new MarketMakerBot(data.TraderId, data.Symbol, data.Role, data.BasePower, data.CreatedAt)
        {
            Id = data.Id,
            IsActive = data.IsActive,
            ActivePower = data.ActivePower
        };
        return marketMakerBot;
    }

    /// <summary>
    /// Обновляет базовую мощность со случайным отклонением, ограниченным заданным диапазоном.
    /// </summary>
    /// <param name="minPower">Минимально допустимое значение мощности.</param>
    /// <param name="maxPower">Максимально допустимое значение мощности.</param>
    public void UpdatePower(decimal minPower, decimal maxPower)
    {
        if (minPower >= maxPower)
            throw new DomainException("Min power must be less than max power");

        var change = Random.Shared.Next(-35, 35);
        BasePower = Math.Clamp(BasePower + change, minPower, maxPower);
    }

    /// <summary>Устанавливает торговую роль этого бота.</summary>
    /// <param name="role">Новая роль маркетмейкера.</param>
    public void SetRole(MarketMakerRole role) => Role = role;

    /// <summary>Устанавливает активный статус этого бота.</summary>
    /// <param name="isActive">Активен ли бот.</param>
    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>
    /// Перемещает бота на указанного (нового/своего) трейдера.
    /// Возвращает идентификатор предыдущего трейдера: контракт метода требует, чтобы
    /// вызывающая сторона после переезда гарантированно обработала активные ордера
    /// предыдущего трейдера (отменила их) — это обязательство домена.
    /// </summary>
    public long MoveToTrader(long newTraderId)
    {
        if (newTraderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newTraderId), "Идентификатор трейдера должен быть положительным.");
        var previousTraderId = TraderId;
        TraderId = newTraderId;
        return previousTraderId;
    }

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
    /// <param name="modifiers">Коллекция модификаторов расчёта мощности для применения.</param>
    public void RebalancePower(IReadOnlyCollection<IPowerCalculationModify> modifiers)
    {
        foreach (var modify in modifiers)
            modify.Apply(this);
    }

    /// <summary>Задает активную мощность (внутренний сеттер для модификаторов мощности).</summary>
    internal void SetActivePower(decimal value) => ActivePower = value;

    /// <summary>Возвращает дефолтные ресурсы бота: баланс трейдера и размер портфеля токенов.</summary>
    /// <returns>Кортеж с балансом трейдера и количеством токенов портфеля.</returns>
    public static (decimal Balance, int PortfolioTokens) GetDefaultResources() => (DefaultBalance, DefaultPortfolioTokens);
}

/// <summary>Снимок сохранённых параметров бота для восстановления из БД.</summary>
internal sealed record MarketMakerBotLoadData(
    long Id,
    long TraderId,
    string Symbol,
    MarketMakerRole Role,
    decimal BasePower,
    bool IsActive,
    DateTime CreatedAt,
    decimal ActivePower);
