using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;

namespace ArkWallet.Tests.Core.TradingContext.Domain.Engines;

public class MarketMakerOrderEngineTest
{
    [Fact]
    public void BuildActiveOrder_Buyer_ReturnsBuyCommand()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);
        var currentPrice = 1000m;

        var cmd = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);

        Assert.Equal("купить", cmd.Direction);
        Assert.Equal(bot.TraderId, cmd.TraderId);
        Assert.Equal("ZZZ", cmd.Symbol);
        Assert.True(cmd.Quantity > 0);
    }

    [Fact]
    public void BuildActiveOrder_Seller_ReturnsSellCommand()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", MarketMakerRole.Seller, 50);
        var currentPrice = 1000m;

        var cmd = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);

        Assert.Equal("продать", cmd.Direction);
        Assert.Equal(bot.TraderId, cmd.TraderId);
        Assert.Equal("ZZZ", cmd.Symbol);
        Assert.True(cmd.Quantity > 0);
    }

    [Fact]
    public void BuildActiveOrder_Buyer_PriceAboveCurrent()
    {
        var bot = MarketMakerBot.Create(201, "XYZ", MarketMakerRole.Buyer, 50);
        var currentPrice = 100m;

        var cmd = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);

        Assert.True(cmd.Price > currentPrice);
        Assert.True(cmd.Price >= currentPrice * 1.19m);
    }

    [Fact]
    public void BuildActiveOrder_Seller_PriceBelowCurrent()
    {
        var bot = MarketMakerBot.Create(202, "XYZ", MarketMakerRole.Seller, 50);
        var currentPrice = 100m;

        var cmd = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);

        Assert.True(cmd.Price < currentPrice);
        Assert.True(cmd.Price <= currentPrice * 0.81m);
    }

    [Theory]
    [InlineData(MarketMakerRole.Buyer)]
    [InlineData(MarketMakerRole.Seller)]
    public void BuildActiveOrder_MinimumQuantityAtLeastOne(MarketMakerRole role)
    {
        var bot = MarketMakerBot.Create(301, "TTT", role, 50);
        var currentPrice = 50m;

        var cmd = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);

        Assert.True(cmd.Quantity >= 1);
    }

    [Fact]
    public void BuildActiveOrder_MultiplierReducesQuantity()
    {
        var bot = MarketMakerBot.Create(401, "ZZZ", MarketMakerRole.Buyer, 50);
        var currentPrice = 1000m;

        var cmdWithout = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice);
        var cmdWith = MarketMakerOrderEngine.BuildActiveOrder(bot, currentPrice, 0.5m);

        Assert.True(cmdWith.Quantity <= cmdWithout.Quantity);
    }
}
