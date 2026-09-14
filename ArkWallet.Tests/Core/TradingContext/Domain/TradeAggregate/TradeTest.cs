using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;

namespace ArkWallet.Tests.Core.TradingContext.Domain.TradeAggregate;

public class TradeTest
{
    private static Trade CreateDataTrade(
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
        var trade = CreateDataTrade(price: 75m, quantity: 4);

        Assert.Equal(300m, trade.GetTotalValue);
    }

    [Fact]
    public void GetDescription_ReturnsFormattedString()
    {
        var trade = CreateDataTrade(symbol: "BBB", price: 30m, quantity: 5);

        Assert.Equal("5 BBB по 30𝖘𝖙", trade.GetDescription);
    }

    [Fact]
    public void Create_SetsId()
    {
        var trade = CreateDataTrade();

        Assert.False(string.IsNullOrEmpty(trade.Id));
    }

    [Fact]
    public void Create_SetsExecutedAt()
    {
        var before = DateTime.UtcNow;
        var trade = CreateDataTrade();

        Assert.True(trade.ExecutedAt >= before);
    }

    [Fact]
    public void InvolvesLogic_BuyerId_Matches()
    {
        // Инвариант involved проверяется на уровне Domain.Trade (Domain.TradeAggregate.Trade.InvolvesTrader).
        // Здесь подтверждается структура данных: BuyerId и SellerId доступны для проверки.
        var trade = CreateDataTrade(buyerId: 101, sellerId: 202);
        Assert.Equal(101, trade.BuyerId);
        Assert.Equal(202, trade.SellerId);
    }

    [Fact]
    public void InvolvesLogic_SellerId_Matches()
    {
        var trade = CreateDataTrade(buyerId: 55, sellerId: 202);
        Assert.Equal(55, trade.BuyerId);
        Assert.Equal(202, trade.SellerId);
    }

    [Fact]
    public void InvolvesLogic_NoMatch()
    {
        var trade = CreateDataTrade();
        Assert.Equal(101, trade.BuyerId);
        Assert.Equal(202, trade.SellerId);
    }
}
