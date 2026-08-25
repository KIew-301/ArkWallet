using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.TradingContext;

internal class PriceCandle
{
    public long Id { get; private set; }
    public decimal OpenPrice { get; }
    public decimal HighPrice { get; private set; }
    public decimal LowPrice { get; private set; }
    public decimal ClosePrice { get; private set; }
    public DateTime Timestamp { get; }

    private PriceCandle(decimal openPrice, DateTime timestamp)
    {
        OpenPrice = openPrice;
        HighPrice = openPrice;
        LowPrice = openPrice;
        ClosePrice = openPrice;
        Timestamp = timestamp;
    }

    public static PriceCandle CreateNew(decimal openPrice, DateTime timestamp)
    {
        if (openPrice < 0)
            throw new DomainException("Open price cannot be negative");

        return new PriceCandle(openPrice, timestamp);
    }

    public void Update(decimal newPrice)
    {
        if (newPrice < 0)
            throw new DomainException("Price cannot be negative");

        if (newPrice > HighPrice)
            HighPrice = newPrice;
        if (newPrice < LowPrice)
            LowPrice = newPrice;

        ClosePrice = newPrice;
    }
}
