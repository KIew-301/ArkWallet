using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.PortfolioContext;

/// <summary>
/// Position aggregate in the Portfolio context. The root of portfolio business rules:
/// buying, reserving, selling, returning and removing tokens as well as value calculations.
/// </summary>
internal class Position
{
    public string Id { get; }
    public long TraderTelegramId { get; }
    public string Symbol { get; }
    public int Quantity { get; private set; }
    public int SellingQuantity { get; private set; }
    public int ReserveQuantity { get; private set; }
    public decimal AverageBuyPrice { get; private set; }
    public decimal AverageSellPrice { get; private set; }
    public decimal AverageReservePrice { get; private set; }
    public DateTime AcquiredAt { get; }

    private Position(
        string id,
        long traderTelegramId,
        string symbol,
        int quantity,
        int sellingQuantity,
        int reserveQuantity,
        decimal averageBuyPrice,
        decimal averageSellPrice,
        decimal averageReservePrice,
        DateTime acquiredAt)
    {
        Id = id;
        TraderTelegramId = traderTelegramId;
        Symbol = symbol;
        Quantity = quantity;
        SellingQuantity = sellingQuantity;
        ReserveQuantity = reserveQuantity;
        AverageBuyPrice = averageBuyPrice;
        AverageSellPrice = averageSellPrice;
        AverageReservePrice = averageReservePrice;
        AcquiredAt = acquiredAt;
    }

    /// <summary>
    /// Rehydrates a Position from its persistence record.
    /// </summary>
    internal static Position Load(
        string id,
        long traderTelegramId,
        string symbol,
        int quantity,
        int sellingQuantity,
        int reserveQuantity,
        decimal averageBuyPrice,
        decimal averageSellPrice,
        decimal averageReservePrice,
        DateTime acquiredAt)
    {
        return new Position(
            id, traderTelegramId, symbol, quantity, sellingQuantity,
            reserveQuantity, averageBuyPrice, averageSellPrice,
            averageReservePrice, acquiredAt);
    }

    /// <summary>
    /// Creates a new position for a trader owning the given quantity at the given buy price.
    /// </summary>
    internal static Position Create(long traderTelegramId, string symbol, int quantity, decimal price)
    {
        if (quantity <= 0)
            throw new DomainException("Для обновление портфеля необходим минимум один токен");

        return new Position(
            id: Guid.NewGuid().ToString(),
            traderTelegramId: traderTelegramId,
            symbol: symbol,
            quantity: quantity,
            sellingQuantity: 0,
            reserveQuantity: 0,
            averageBuyPrice: price,
            averageSellPrice: 0,
            averageReservePrice: 0,
            acquiredAt: DateTime.UtcNow);
    }

    /// <summary>
    /// Total value of the position based on the average buy price.
    /// </summary>
    public decimal GetTotalValue() => Quantity * AverageBuyPrice;

    /// <summary>
    /// Current market value of the position.
    /// </summary>
    public decimal GetCurrentValue(decimal currentPrice) => Quantity * currentPrice;

    /// <summary>
    /// Profit or loss of the position against the current market price.
    /// </summary>
    public decimal GetProfitLoss(decimal currentPrice) => GetCurrentValue(currentPrice) - GetTotalValue();

    /// <summary>
    /// Buys the given quantity, recalculating the average buy price.
    /// </summary>
    public void BuyTokens(int quantity, decimal buyPrice)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

        var totalCost = Quantity * AverageBuyPrice + quantity * buyPrice;
        Quantity += quantity;
        AverageBuyPrice = totalCost / Quantity;
    }

    /// <summary>
    /// Moves the given quantity from the available balance into reserve.
    /// </summary>
    public void ReserveTokens(int quantity, decimal reservePrice)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

        Quantity -= quantity;

        var totalCost = ReserveQuantity * AverageReservePrice + quantity * reservePrice;
        ReserveQuantity += quantity;
        AverageReservePrice = totalCost / ReserveQuantity;

        if (Quantity == 0)
            AverageBuyPrice = 0;
    }

    /// <summary>
    /// Moves the given quantity from reserve into sold state.
    /// </summary>
    public void SellTokens(int quantity, decimal sellPrice)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

        ReserveQuantity -= quantity;

        var totalCost = SellingQuantity * AverageSellPrice + quantity * sellPrice;
        SellingQuantity += quantity;
        AverageSellPrice = totalCost / SellingQuantity;

        if (ReserveQuantity == 0)
            AverageReservePrice = 0;
    }

    /// <summary>
    /// Returns the given quantity from reserve back to the available balance.
    /// </summary>
    public void ReturnTokens(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

        ReserveQuantity -= quantity;

        var totalCost = Quantity * AverageBuyPrice + quantity * AverageReservePrice;
        Quantity += quantity;
        AverageBuyPrice = totalCost / Quantity;

        if (ReserveQuantity == 0)
            AverageReservePrice = 0;
    }

    /// <summary>
    /// Removes the given quantity from the available balance (e.g. gifted or transferred away).
    /// </summary>
    public void RemoveTokens(int quantity, decimal buyPrice)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше или равно 0");
        if (quantity > Quantity) throw new DomainException("Больше токенов недостаточно");

        Quantity -= quantity;

        if (Quantity == 0)
            AverageBuyPrice = 0;
    }

    /// <summary>
    /// Replaces the full position state (used when persisting the trading engine result).
    /// </summary>
    public void ApplyState(
        int quantity,
        int sellingQuantity,
        int reserveQuantity,
        decimal averageBuyPrice,
        decimal averageSellPrice,
        decimal averageReservePrice)
    {
        Quantity = quantity;
        SellingQuantity = sellingQuantity;
        ReserveQuantity = reserveQuantity;
        AverageBuyPrice = averageBuyPrice;
        AverageSellPrice = averageSellPrice;
        AverageReservePrice = averageReservePrice;
    }

    /// <summary>
    /// Whether the position holds no tokens in any state.
    /// </summary>
    public bool IsEmpty => Quantity == 0 && ReserveQuantity == 0 && SellingQuantity == 0;

    /// <summary>
    /// Sets the position to the given quantity, buying the shortfall or releasing the surplus. All
    /// decisions about how to reconcile the difference live here in the aggregate.
    /// </summary>
    public void CreateOrUpdate(int quantity, decimal price)
    {
        var diff = Quantity - quantity;
        if (diff < 0)
            BuyTokens(-diff, price);
        else if (diff > 0)
        {
            ReserveTokens(diff, price);
            SellTokens(diff, price);
        }
    }

    /// <summary>
    /// Adds tokens to the available balance without changing the average buy price,
    /// as if the position had always held more tokens.
    /// </summary>
    public void AddTokens(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Количество токенов меньше или равно 0");

        Quantity += quantity;
    }

    /// <summary>
    /// Applies a single portfolio mutation described by the command. The aggregate decides which
    /// operation runs and throws on any invalid transition.
    /// </summary>
    public void ChangePosition(PortfolioChangeCommand command)
    {
        switch (command.Type)
        {
            case PortfolioChangeType.Buy:
                BuyTokens(command.Quantity, command.Price);
                break;
            case PortfolioChangeType.Reserve:
                ReserveTokens(command.Quantity, command.Price);
                break;
            case PortfolioChangeType.Sell:
                SellTokens(command.Quantity, command.Price);
                break;
            case PortfolioChangeType.Return:
                ReturnTokens(command.Quantity);
                break;
            case PortfolioChangeType.Remove:
                RemoveTokens(command.Quantity, command.Price);
                break;
            case PortfolioChangeType.Add:
                AddTokens(command.Quantity);
                break;
            default:
                throw new DomainException("Неизвестная операция над портфелем");
        }
    }
}
