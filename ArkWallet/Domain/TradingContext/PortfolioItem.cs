using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

internal class PortfolioItem : AggregateRoot
{
    public string Id { get; internal set; } = Guid.NewGuid().ToString();
    public long TraderId { get; }
    public string TokenSymbol { get; }
    public int Quantity { get; private set; }
    public int SellingQuantity { get; private set; }
    public int ReserveQuantity { get; private set; }
    public decimal AverageBuyPrice { get; private set; }
    public decimal AverageSellPrice { get; private set; }
    public decimal AverageReservePrice { get; private set; }
    public DateTime AcquiredAt { get; }

    private PortfolioItem(long traderId, string tokenSymbol, int quantity, decimal buyPrice, DateTime acquiredAt)
    {
        TraderId = traderId;
        TokenSymbol = tokenSymbol;
        Quantity = quantity;
        AverageBuyPrice = buyPrice;
        AcquiredAt = acquiredAt;
    }

    public static PortfolioItem Create(long traderId, string tokenSymbol, int quantity, decimal buyPrice, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (buyPrice < 0)
            throw new DomainException("Buy price cannot be negative");

        var acquiredAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var item = new PortfolioItem(traderId, tokenSymbol, quantity, buyPrice, acquiredAt);
        return item;
    }

    internal static PortfolioItem Load(
        long traderId,
        string id,
        string tokenSymbol,
        int quantity,
        int sellingQuantity,
        int reserveQuantity,
        decimal averageBuyPrice,
        decimal averageSellPrice,
        decimal averageReservePrice,
        DateTime acquiredAt)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new DomainException("Token symbol cannot be empty");

        return new PortfolioItem(traderId, tokenSymbol, quantity, averageBuyPrice, acquiredAt)
        {
            Id = id,
            SellingQuantity = sellingQuantity,
            ReserveQuantity = reserveQuantity,
            AverageSellPrice = averageSellPrice,
            AverageReservePrice = averageReservePrice
        };
    }

    public decimal GetTotalValue() => Quantity * AverageBuyPrice;

    public decimal GetCurrentValue(decimal currentPrice) => Quantity * currentPrice;

    public decimal GetProfitLoss(decimal currentPrice) => GetCurrentValue(currentPrice) - GetTotalValue();

    public void BuyTokens(int quantity, decimal buyPrice)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (buyPrice < 0)
            throw new DomainException("Buy price cannot be negative");

        var totalCost = Quantity * AverageBuyPrice + quantity * buyPrice;
        Quantity += quantity;
        AverageBuyPrice = totalCost / Quantity;
    }

    public void ReserveTokens(int quantity, decimal reservePrice)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (quantity > Quantity)
            throw new DomainException("Not enough tokens in portfolio");

        Quantity -= quantity;

        var totalCost = ReserveQuantity * AverageReservePrice + quantity * reservePrice;
        ReserveQuantity += quantity;
        AverageReservePrice = totalCost / ReserveQuantity;

        if (Quantity == 0)
            AverageBuyPrice = 0;

    }

    public void SellTokens(int quantity, decimal sellPrice)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (quantity > ReserveQuantity)
            throw new DomainException("Not enough reserved tokens");

        ReserveQuantity -= quantity;

        var totalCost = SellingQuantity * AverageSellPrice + quantity * sellPrice;
        SellingQuantity += quantity;
        AverageSellPrice = totalCost / SellingQuantity;

        if (ReserveQuantity == 0)
            AverageReservePrice = 0;

    }

    public void ReturnTokens(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (quantity > ReserveQuantity)
            throw new DomainException("Not enough reserved tokens");

        ReserveQuantity -= quantity;

        var totalCost = Quantity * AverageBuyPrice + quantity * AverageReservePrice;
        Quantity += quantity;
        AverageBuyPrice = totalCost / Quantity;

        if (ReserveQuantity == 0)
            AverageReservePrice = 0;

    }

    public void RemoveTokens(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        if (quantity > Quantity)
            throw new DomainException("Not enough tokens in portfolio");

        Quantity -= quantity;

        if (Quantity == 0)
            AverageBuyPrice = 0;

    }
}
