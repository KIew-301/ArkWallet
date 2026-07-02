using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests.EngineTests;

public class MarketMakerGridEngineTest
{
    [Fact]
    public void GetOrdersToPlace_BuyerRole_ReturnsCommands()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.Equal("купить", c.Direction));
        Assert.All(commands, c => Assert.Equal(bot.TraderId, c.TraderId));
        Assert.All(commands, c => Assert.Equal("ZZZ", c.Symbol));
        Assert.All(commands, c => Assert.True(c.Quantity > 0));
        Assert.All(commands, c => Assert.True(c.Price < currentPrice));
        Assert.All(commands, c => Assert.True(c.Price >= currentPrice * 0.8m));
    }

    [Fact]
    public void GetOrdersToPlace_SellerRole_ReturnsCommands()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.Equal("продать", c.Direction));
        Assert.All(commands, c => Assert.Equal(bot.TraderId, c.TraderId));
        Assert.All(commands, c => Assert.Equal("ZZZ", c.Symbol));
        Assert.All(commands, c => Assert.True(c.Quantity > 0));
        Assert.All(commands, c => Assert.True(c.Price > currentPrice));
        Assert.All(commands, c => Assert.True(c.Price <= currentPrice * 1.2m));
    }

    [Fact]
    public void GetOrdersToPlace_WhenOrdersExistInAllRanges_ReturnsEmpty()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        for (decimal price = 1000; price >= 800; price /= 1.001m)
        {
            var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, price, 5);
            existingOrders.Add(order);
        }

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Empty(commands);
    }

    [Fact]
    public void GetOrdersToPlace_BuyerGrid_PriceWithinBounds()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders, stepsCount: 20, minPricePercent: 0.8m);

        foreach (var command in commands)
        {
            Assert.True(command.Price >= 800m, $"Price {command.Price} should be >= 800");
            Assert.True(command.Price < 1000m, $"Price {command.Price} should be < 1000");
        }
    }

    [Fact]
    public void GetOrdersToPlace_SellerGrid_PriceWithinBounds()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders, stepsCount: 20, maxPricePercent: 1.2m);

        foreach (var command in commands)
        {
            Assert.True(command.Price > 1000m, $"Price {command.Price} should be > 1000");
            Assert.True(command.Price <= 1200m, $"Price {command.Price} should be <= 1200");
        }
    }

    [Fact]
    public void GetOrdersToPlace_DifferentBotPower_QuantityScales()
    {
        var engine = new MarketMakerGridEngine();

        var botWeak = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 10);
        var botStrong = MarketMakerBot.Create(102, "ZZZ", BotRole.Buyer, 100);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commandsWeak = engine.GetOrdersToPlace(botWeak, currentPrice, existingOrders);
        var commandsStrong = engine.GetOrdersToPlace(botStrong, currentPrice, existingOrders);

        var avgWeak = commandsWeak.Average(c => c.Quantity);
        var avgStrong = commandsStrong.Average(c => c.Quantity);

        Assert.True(avgStrong > avgWeak);
    }

    [Fact]
    public void GetOrdersToPlace_OnlyMissingRanges_Filled()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var existingOrder = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 985m, 5);
        existingOrders.Add(existingOrder);

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.NotEqual(985m, c.Price));
    }

    [Fact]
    public void GetOrdersToPlace_ExistingOrdersIgnoredIfInactive()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 985m, 5);
        order.Cancel(101);
        existingOrders.Add(order);

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Contains(commands, c => Math.Abs(c.Price - 985m) < 1m);
    }

    [Fact]
    public void GetOrdersToPlace_DefaultParameters_WorkCorrectly()
    {
        var engine = new MarketMakerGridEngine();
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Equal(20, commands.Count);
        Assert.All(commands, c => Assert.True(c.Price >= 800m));
        Assert.All(commands, c => Assert.True(c.Price < 1000m));
    }
}