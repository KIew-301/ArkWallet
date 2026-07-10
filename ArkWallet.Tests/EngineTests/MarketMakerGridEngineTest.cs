using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests.EngineTests;

public class MarketMakerGridEngineTest
{
    private readonly MarketMakerGridEngine _engine = new(new FixedGridEngine());

    [Fact]
    public void GetOrdersToPlace_BuyerRole_ReturnsCommands()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

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
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

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
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var grid = new FixedGridEngine().GetGridBelowPrice(currentPrice, 21);

        for (int i = 0; i < grid.Count - 1; i++)
        {
            var lower = grid[i + 1];
            var upper = grid[i];
            var price = (lower + upper) / 2;

            var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, price, 5);
            existingOrders.Add(order);
        }

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Empty(commands);
    }

    [Fact]
    public void GetOrdersToPlace_BuyerGrid_PriceWithinBounds()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders, stepsCount: 20);

        foreach (var command in commands)
        {
            Assert.True(command.Price >= 800m, $"Price {command.Price} should be >= 800");
            Assert.True(command.Price < 1000m, $"Price {command.Price} should be < 1000");
        }
    }

    [Fact]
    public void GetOrdersToPlace_SellerGrid_PriceWithinBounds()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders, stepsCount: 20);

        foreach (var command in commands)
        {
            Assert.True(command.Price > 1000m, $"Price {command.Price} should be > 1000");
            Assert.True(command.Price <= 1200m, $"Price {command.Price} should be <= 1200");
        }
    }

    [Fact]
    public void GetOrdersToPlace_DifferentBotPower_QuantityScales()
    {
        var botWeak = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 10);
        var botStrong = MarketMakerBot.Create(102, "ZZZ", BotRole.Buyer, 100);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commandsWeak = _engine.GetOrdersToPlace(botWeak, currentPrice, existingOrders);
        var commandsStrong = _engine.GetOrdersToPlace(botStrong, currentPrice, existingOrders);

        var avgWeak = commandsWeak.Average(c => c.Quantity);
        var avgStrong = commandsStrong.Average(c => c.Quantity);

        Assert.True(avgStrong > avgWeak);
    }

    [Fact]
    public void GetOrdersToPlace_OnlyMissingRanges_Filled()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var existingOrder = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 985m, 5);
        existingOrders.Add(existingOrder);

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.NotEqual(985m, c.Price));
    }

    [Fact]
    public void GetOrdersToPlace_ExistingOrdersIgnoredIfInactive()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 985m, 5);
        order.Cancel(101);
        existingOrders.Add(order);

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Contains(commands, c => Math.Abs(c.Price - 985m) < 1m);
    }

    [Fact]
    public void GetOrdersToPlace_DefaultParameters_WorkCorrectly()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<TradeOrder>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Equal(20, commands.Count);
        Assert.All(commands, c => Assert.True(c.Price >= 800m));
        Assert.All(commands, c => Assert.True(c.Price < 1000m));
    }

    [Fact]
    public void GetOrdersToPlace_WhenOrderExistsInRange_ShouldNotCreateDuplicate()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        var currentPrice = 100m;

        var existingOrder = TradeOrder.Create(
            OrderType.Buy,
            "ZZZ",
            101,
            99.5m,
            5
        );

        var existingOrders = new List<TradeOrder> { existingOrder };

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        var createdForPrice = commands.Any(c => Math.Abs(c.Price - 99.5m) < 0.01m);
        Assert.False(createdForPrice, "Движок создал дублирующий ордер, хотя существующий уже есть в диапазоне");
    }

    [Fact]
    public void GetOrdersToPlace_WhenPriceRounding_ShouldStillDetectExistingOrder()
    {
        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        var currentPrice = 100m;

        var existingOrder = TradeOrder.Create(
            OrderType.Buy,
            "ZZZ",
            101,
            98.7654321m,
            5
        );

        var existingOrders = new List<TradeOrder> { existingOrder };

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        var createdForPrice = commands.Any(c => Math.Abs(c.Price - 98.7654321m) < 0.01m);
        Assert.False(createdForPrice, "Движок не распознал существующий ордер из-за округления");
    }
}