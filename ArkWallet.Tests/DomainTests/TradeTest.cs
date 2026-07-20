using ArkWallet.Domain.Entities;

namespace ArkWallet.Tests.DomainTests;

public class TradeTest
{
    private static Trade CreateTrade(
        long buyerId = 101,
        long sellerId = 202,
        string symbol = "AAA",
        decimal price = 50m,
        int quantity = 10) =>
        new()
        {
            BuyerId = buyerId,
            SellerId = sellerId,
            CharacterTokenId = symbol,
            Price = price,
            Quantity = quantity
        };

    [Fact]
    public void GetTotalValue_ReturnsPriceTimesQuantity()
    {
        var trade = CreateTrade(price: 75m, quantity: 4);

        Assert.Equal(300m, trade.GetTotalValue());
    }

    [Fact]
    public void InvolvesTrader_BuyerId_ReturnsTrue()
    {
        var trade = CreateTrade(buyerId: 101);

        Assert.True(trade.InvolvesTrader(101));
    }

    [Fact]
    public void InvolvesTrader_SellerId_ReturnsTrue()
    {
        var trade = CreateTrade(sellerId: 202);

        Assert.True(trade.InvolvesTrader(202));
    }

    [Fact]
    public void InvolvesTrader_UnknownId_ReturnsFalse()
    {
        var trade = CreateTrade();

        Assert.False(trade.InvolvesTrader(999));
    }

    [Fact]
    public void GetDescription_ReturnsFormattedString()
    {
        var trade = CreateTrade(symbol: "BBB", price: 30m, quantity: 5);

        Assert.Equal("5 BBB по 30₽", trade.GetDescription());
    }

    [Fact]
    public void Create_SetsId()
    {
        var trade = CreateTrade();

        Assert.False(string.IsNullOrEmpty(trade.Id));
    }

    [Fact]
    public void Create_SetsExecutedAt()
    {
        var before = DateTime.UtcNow;
        var trade = CreateTrade();

        Assert.True(trade.ExecutedAt >= before);
    }
}
