using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

internal class Trader : AggregateRoot
{
    private const decimal DefaultBalance = 1000m;

    private readonly List<PortfolioItem> _portfolio = new();
    private readonly List<Order> _orders = new();

    public long Id { get; }
    public string? Username { get; private set; }
    public decimal Balance { get; private set; }
    public bool NotificationOn { get; private set; }
    public DateTime JoinedAt { get; }

    public IReadOnlyList<PortfolioItem> Portfolio => _portfolio;
    public IReadOnlyList<Order> Orders => _orders;

    private Trader(long id, string? username, decimal initialBalance, DateTime joinedAt)
    {
        Id = id;
        Username = username;
        Balance = initialBalance;
        NotificationOn = true;
        JoinedAt = joinedAt;
    }

    public static Trader Create(long id, string? username, decimal initialBalance = DefaultBalance, TimeProvider? timeProvider = null)
    {
        if (initialBalance < 0)
            throw new DomainException("Initial balance cannot be negative");

        var joinedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return new Trader(id, username, initialBalance, joinedAt);
    }

    internal static Trader Load(
        long id,
        string? username,
        decimal balance,
        bool notificationOn,
        DateTime joinedAt)
    {
        var trader = new Trader(id, username, balance, joinedAt);
        trader.NotificationOn = notificationOn;
        return trader;
    }

    internal void AttachPortfolio(PortfolioItem item) => _portfolio.Add(item);

    internal void AttachOrder(Order order)
    {
        order.TraderId = Id;
        _orders.Add(order);
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Deposit amount must be greater than 0");

        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Withdraw amount must be greater than 0");

        if (Balance < amount)
            throw new DomainException("Insufficient balance");

        Balance -= amount;
    }

    public void SetUsername(string? username) => Username = username;

    public void SetNotification(bool enabled) => NotificationOn = enabled;

    public void BuyTokens(string tokenSymbol, int quantity, decimal price)
    {
        Withdraw(quantity * price);
        AddToPortfolio(tokenSymbol, quantity, price);
    }

    public void SellTokens(string tokenSymbol, int quantity, decimal price)
    {
        var item = GetPortfolioItem(tokenSymbol);
        item.ReserveTokens(quantity, price);
        item.SellTokens(quantity, price);
        Deposit(quantity * price);
    }

    public void RemoveTokens(string tokenSymbol, int quantity)
    {
        var item = GetPortfolioItem(tokenSymbol);
        item.RemoveTokens(quantity);
    }

    public async Task<Order> PlaceOrder(OrderType type, string tokenSymbol, decimal price, int quantity)
    {
        var order = Order.Create(type, tokenSymbol, price, quantity);
        order.TraderId = Id;
        order.SetEventPublisher(EventPublisher);

        if (type == OrderType.Sell)
        {
            var item = GetPortfolioItem(tokenSymbol);
            item.ReserveTokens(quantity, price);
        }
        else
        {
            Withdraw(order.GetReservedBalance());
        }

        _orders.Add(order);
        await PublishAsync(new OrderPlacedEvent(order));
        return order;
    }

    public void CancelOrder(string orderId)
    {
        var order = GetOrder(orderId);
        order.Cancel();

        if (order.Type == OrderType.Sell)
        {
            var item = GetPortfolioItem(order.TokenSymbol);
            item.ReturnTokens(order.GetRemainingQuantity());
        }
    }

    public async Task FillOrder(string orderId, int filledQuantity, decimal price)
    {
        var order = GetOrder(orderId);

        if (!order.IsActive())
            throw new DomainException("Only active orders can be filled");
        if (filledQuantity <= 0)
            throw new DomainException("Filled quantity must be greater than 0");
        if (filledQuantity > order.GetRemainingQuantity())
            throw new DomainException("Filled quantity exceeds remaining quantity");
        if (price < 0)
            throw new DomainException("Price cannot be negative");

        if (order.Type == OrderType.Buy)
        {
            var overpayment = (order.Price - price) * filledQuantity;
            if (overpayment > 0)
                Deposit(overpayment);

            AddToPortfolio(order.TokenSymbol, filledQuantity, price);
        }
        else
        {
            var item = GetPortfolioItem(order.TokenSymbol);
            if (item.ReserveQuantity < filledQuantity)
                throw new DomainException("Not enough reserved tokens");

            item.SellTokens(filledQuantity, price);
            Deposit(filledQuantity * price);
        }

        await order.UpdateOrderFill(filledQuantity, price);
    }

    private void AddToPortfolio(string tokenSymbol, int quantity, decimal price)
    {
        var item = _portfolio.FirstOrDefault(p => p.TokenSymbol == tokenSymbol);
        if (item is null)
        {
            _portfolio.Add(PortfolioItem.Create(Id, tokenSymbol, quantity, price));
        }
        else
        {
            item.BuyTokens(quantity, price);
        }
    }

    private PortfolioItem GetPortfolioItem(string tokenSymbol)
        => _portfolio.FirstOrDefault(p => p.TokenSymbol == tokenSymbol)
           ?? throw new DomainException($"No portfolio item for token {tokenSymbol}");

    private Order GetOrder(string orderId)
        => _orders.FirstOrDefault(o => o.Id == orderId)
           ?? throw new DomainException($"Order {orderId} not found");
}
