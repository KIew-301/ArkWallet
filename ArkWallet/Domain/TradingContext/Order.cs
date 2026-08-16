using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

/// <summary>Тип заявки: покупка или продажа.</summary>
public enum OrderType
{
    /// <summary>Заявка на покупку.</summary>
    Buy,

    /// <summary>Заявка на продажу.</summary>
    Sell
}

/// <summary>Статус заявки.</summary>
public enum OrderStatus
{
    /// <summary>Заявка активна и ожидает исполнения.</summary>
    Active,

    /// <summary>Заявка полностью исполнена.</summary>
    Filled,

    /// <summary>Заявка отменена.</summary>
    Cancelled,

    /// <summary>Заявка истекла.</summary>
    Expired
}

internal class Order : AggregateRoot
{
    public string Id { get; internal set; } = Guid.NewGuid().ToString();
    public long TraderId { get; internal set; }
    public OrderType Type { get; }
    public OrderStatus Status { get; private set; }
    public string TokenSymbol { get; }
    public decimal Price { get; }
    public decimal AverageExecutePrice { get; private set; }
    public int Quantity { get; }
    public int FilledQuantity { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ExecutedAt { get; private set; }

    private Order(OrderType type, string tokenSymbol, decimal price, int quantity, DateTime createdAt)
    {
        Type = type;
        TokenSymbol = tokenSymbol;
        Price = price;
        Quantity = quantity;
        Status = OrderStatus.Active;
        CreatedAt = createdAt;
    }

    public static Order Create(OrderType type, string tokenSymbol, decimal price, int quantity, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");
        if (price <= 0)
            throw new DomainException("Price must be greater than 0");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new Order(type, tokenSymbol, price, quantity, createdAt);
    }

    internal static Order Load(
        string id,
        OrderType type,
        OrderStatus status,
        string tokenSymbol,
        decimal price,
        decimal averageExecutePrice,
        int quantity,
        int filledQuantity,
        DateTime createdAt,
        DateTime? executedAt)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");

        return new Order(type, tokenSymbol, price, quantity, createdAt)
        {
            Id = id,
            Status = status,
            AverageExecutePrice = averageExecutePrice,
            FilledQuantity = filledQuantity,
            ExecutedAt = executedAt
        };
    }

    public bool IsFilled() => FilledQuantity >= Quantity;

    public bool IsActive() => Status == OrderStatus.Active;

    public bool IsLong() => Type == OrderType.Buy;

    public bool IsShort() => Type == OrderType.Sell;

    public int GetRemainingQuantity() => Quantity - FilledQuantity;

    public decimal GetReservedBalance() => GetRemainingQuantity() * Price;

    public async Task UpdateOrderFill(int filledQuantity, decimal price)
    {
        if (!IsActive())
            throw new DomainException("Only active orders can be filled");
        if (filledQuantity <= 0)
            throw new DomainException("Filled quantity must be greater than 0");
        if (filledQuantity > GetRemainingQuantity())
            throw new DomainException("Filled quantity exceeds remaining quantity");
        if (price < 0)
            throw new DomainException("Price cannot be negative");

        var totalCost = FilledQuantity * AverageExecutePrice + filledQuantity * price;
        FilledQuantity += filledQuantity;
        AverageExecutePrice = totalCost / FilledQuantity;

        if (IsFilled())
            MarkAsFilled();

        await PublishAsync(new OrderFilledEvent(this));
    }

    public void Cancel()
    {
        if (!IsActive())
            throw new DomainException("Only active orders can be cancelled");

        Status = OrderStatus.Cancelled;
    }

    public Order WithQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        return new Order(Type, TokenSymbol, Price, newQuantity, DateTime.UtcNow);
    }

    private void MarkAsFilled()
    {
        Status = OrderStatus.Filled;
        ExecutedAt = DateTime.UtcNow;
        FilledQuantity = Quantity;
    }
}
