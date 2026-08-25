using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

internal class Trade : AggregateRoot
{
    public string Id { get; }
    public long BuyerId { get; }
    public long SellerId { get; }
    public string TokenSymbol { get; }
    public decimal Price { get; }
    public int Quantity { get; }
    public DateTime ExecutedAt { get; }

    private Trade(string id, long buyerId, long sellerId, string tokenSymbol, decimal price, int quantity, DateTime executedAt)
    {
        Id = id;
        BuyerId = buyerId;
        SellerId = sellerId;
        TokenSymbol = tokenSymbol;
        Price = price;
        Quantity = quantity;
        ExecutedAt = executedAt;
    }

    public static async Task<Trade> Create(
        long buyerId,
        long sellerId,
        string tokenSymbol,
        decimal price,
        int quantity,
        IEventPublisher eventPublisher,
        TimeProvider? timeProvider = null)
    {
        if (buyerId == sellerId)
            throw new DomainException("Buyer and seller cannot be the same trader");
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");
        if (price <= 0)
            throw new DomainException("Price must be greater than 0");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        var executedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var trade = new Trade(Guid.NewGuid().ToString(), buyerId, sellerId, tokenSymbol, price, quantity, executedAt);
        await eventPublisher.PublishAsync(new TradeExecutedEvent(trade));
        return trade;
    }

    public decimal GetTotalValue() => Price * Quantity;

    public bool InvolvesTrader(long traderId) => BuyerId == traderId || SellerId == traderId;
}
