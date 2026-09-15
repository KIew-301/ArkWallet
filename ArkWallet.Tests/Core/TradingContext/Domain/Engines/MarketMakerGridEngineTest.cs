using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;

namespace ArkWallet.Tests.Core.TradingContext.Domain.Engines;

public class MarketMakerGridEngineTest
{
    private readonly MarketMakerGridEngine _engine = new(new FixedGridEngine());

    [Fact]
    public void GetOrdersToPlace_BuyerRole_ReturnsCommands()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

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
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

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
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

        var grid = new FixedGridEngine().GetGridBelowPrice(currentPrice, 21);

        for (int i = 0; i < grid.Count - 1; i++)
        {
            var lower = grid[i + 1];
            var upper = grid[i];
            var price = (lower + upper) / 2;

            var order = Order.Create(OrderType.Buy, "ZZZ", price, 5);
            existingOrders.Add(order);
        }

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Empty(commands);
    }

    [Fact]
    public void GetOrdersToPlace_BuyerGrid_PriceWithinBounds()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

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
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Seller, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

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
        var botWeak = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 10);
        var botStrong = MarketMaker.Create(102, "ZZZ", MarketMakerRole.Buyer, 100);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

        var commandsWeak = _engine.GetOrdersToPlace(botWeak, currentPrice, existingOrders);
        var commandsStrong = _engine.GetOrdersToPlace(botStrong, currentPrice, existingOrders);

        var avgWeak = commandsWeak.Average(c => c.Quantity);
        var avgStrong = commandsStrong.Average(c => c.Quantity);

        Assert.True(avgStrong > avgWeak);
    }

    [Fact]
    public void GetOrdersToPlace_OnlyMissingRanges_Filled()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

        var existingOrder = Order.Create(OrderType.Buy, "ZZZ", 985m, 5);
        existingOrders.Add(existingOrder);

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.NotEqual(985m, c.Price));
    }

    [Fact]
    public void GetOrdersToPlace_ExistingOrdersIgnoredIfInactive()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

        var order = Order.Create(OrderType.Buy, "ZZZ", 985m, 5);
        order.Cancel();
        existingOrders.Add(order);

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Contains(commands, c => Math.Abs(c.Price - 985m) < 1m);
    }

    [Fact]
    public void GetOrdersToPlace_DefaultParameters_WorkCorrectly()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 50);

        var currentPrice = 1000m;
        var existingOrders = new List<Order>();

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        Assert.Equal(20, commands.Count);
        Assert.All(commands, c => Assert.True(c.Price >= 800m));
        Assert.All(commands, c => Assert.True(c.Price < 1000m));
    }

    [Fact]
    public void GetOrdersToPlace_WhenOrderExistsInRange_ShouldNotCreateDuplicate()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 20);
        var currentPrice = 100m;

        var existingOrder = Order.Create(
            OrderType.Buy,
            "ZZZ",
            99.5m,
            5
        );

        var existingOrders = new List<Order> { existingOrder };

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        var createdForPrice = commands.Any(c => Math.Abs(c.Price - 99.5m) < 0.01m);
        Assert.False(createdForPrice, "Движок создал дублирующий ордер, хотя существующий уже есть в диапазоне");
    }

    [Fact]
    public void GetOrdersToPlace_WhenPriceRounding_ShouldStillDetectExistingOrder()
    {
        var bot = MarketMaker.Create(101, "ZZZ", MarketMakerRole.Buyer, 20);
        var currentPrice = 100m;

        var existingOrder = Order.Create(
            OrderType.Buy,
            "ZZZ",
            98.7654321m,
            5
        );

        var existingOrders = new List<Order> { existingOrder };

        var commands = _engine.GetOrdersToPlace(bot, currentPrice, existingOrders);

        var createdForPrice = commands.Any(c => Math.Abs(c.Price - 98.7654321m) < 0.01m);
        Assert.False(createdForPrice, "Движок не распознал существующий ордер из-за округления");
    }
}
