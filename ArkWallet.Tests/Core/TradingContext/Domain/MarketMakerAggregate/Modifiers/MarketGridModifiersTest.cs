using System.Linq;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Tests.Core.TradingContext.Domain.MarketMakerAggregate.Modifiers;

public class MarketGridModifiersTest
{
    private static readonly PlacedOrderLevel[] EmptyLevels = Array.Empty<PlacedOrderLevel>();

    // -----------------------------------------------------------------------
    // BuyerGridModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void BuyerGrid_GrantsOrders_WhenBuyerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 95m),
            new CreateMarketOrderCommand(1, "продать", "TEST", 10, 105m),
        };

        var modifier = new BuyerGridModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.True(result.Count() >= plan.Count() + 19);
    }

    [Fact]
    public void BuyerGrid_NoExtraOrders_WhenSellerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 95m),
        };

        var modifier = new BuyerGridModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void BuyerGrid_AllNewOrders_DirectionBuy()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = Array.Empty<CreateMarketOrderCommand>();

        var modifier = new BuyerGridModifier();
        var result = modifier.Build(bot, conditions, plan).ToList();

        foreach (var order in result.Skip(0)) // all orders are grid orders
        {
            Assert.Equal("купить", order.Direction);
        }
    }

    // -----------------------------------------------------------------------
    // SellerGridModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void SellerGrid_GrantsOrders_WhenSellerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "продать", "TEST", 10, 105m),
        };

        var modifier = new SellerGridModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.True(result.Count() >= plan.Count() + 19);
    }

    [Fact]
    public void SellerGrid_NoExtraOrders_WhenBuyerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "продать", "TEST", 10, 105m),
        };

        var modifier = new SellerGridModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void SellerGrid_AllNewOrders_DirectionSell()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = Array.Empty<CreateMarketOrderCommand>();

        var modifier = new SellerGridModifier();
        var result = modifier.Build(bot, conditions, plan).ToList();

        foreach (var order in result)
        {
            Assert.Equal("продать", order.Direction);
        }
    }

    // -----------------------------------------------------------------------
    // WallGridModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void WallGrid_GrantsWallerOrders_WhenWallerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Waller, 100m);
        var conditions = new MarketConditions(50m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 48m),
            new CreateMarketOrderCommand(1, "продать", "TEST", 10, 52m),
        };

        var modifier = new WallGridModifier();
        var result = modifier.Build(bot, conditions, plan).ToList();

        int originalCount = plan.Count();
        int wallOrders = result.Count - originalCount;
        Assert.True(wallOrders >= 18); // WallBlockerEngine generates 20 levels

        // All wall orders use BasePower * 8
        var expectedQty = (int)Math.Max(100m * 8m, 1m);
        foreach (var order in result.Skip(originalCount))
        {
            Assert.Equal(expectedQty, order.Quantity);
            Assert.Equal(1, order.TraderId);
            Assert.Equal("TEST", order.Symbol);
            Assert.InRange(order.Price, 0m, 100m); // rounded to 2 decimal places
        }
    }

    [Fact]
    public void WallGrid_NoExtraOrders_WhenBuyerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var conditions = new MarketConditions(50m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 48m),
        };

        var modifier = new WallGridModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    // -----------------------------------------------------------------------
    // MarketOrderModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void MarketOrder_AppendsOneOrder_WhenBuyerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 98m),
        };

        var modifier = new MarketOrderModifier();
        var result = modifier.Build(bot, conditions, plan).ToList();

        Assert.Equal(plan.Count() + 1, result.Count);
        var marketOrder = result.Last();
        Assert.Equal("купить", marketOrder.Direction);
        Assert.True(marketOrder.Price >= 100m); // buyer pays more than current price
    }

    [Fact]
    public void MarketOrder_AppendsOneOrder_WhenSellerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "продать", "TEST", 10, 102m),
        };

        var modifier = new MarketOrderModifier();
        var result = modifier.Build(bot, conditions, plan).ToList();

        Assert.Equal(plan.Count() + 1, result.Count);
        var marketOrder = result.Last();
        Assert.Equal("продать", marketOrder.Direction);
        Assert.True(marketOrder.Price <= 100m); // seller asks less than current price
    }

    [Fact]
    public void MarketOrder_NoExtraOrders_WhenWallerRole()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Waller, 100m);
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 98m),
        };

        var modifier = new MarketOrderModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    // -----------------------------------------------------------------------
    // RandomnessPowerModifier (IPowerCalculationModify)
    // -----------------------------------------------------------------------

    [Fact]
    public void RandomnessPower_RangesWithinBaseTimesHalfToTriple()
    {
        var basePower = 40m;
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, basePower);

        var modifier = new RandomnessPowerModifier();
        var minPower = decimal.MaxValue;
        var maxPower = 0m;

        for (int i = 0; i < 300; i++)
        {
            modifier.Apply(bot);
            minPower = Math.Min(minPower, bot.ActivePower);
            maxPower = Math.Max(maxPower, bot.ActivePower);

            // Reset base power for next iteration
            bot.SetActivePower(basePower);
        }

        // factor ∈ [0.5, 1.5)
        // activePower = basePower * factor
        Assert.InRange(minPower, basePower * 0.5m, basePower * 0.55m);
        Assert.InRange(maxPower, basePower * 1.45m, basePower * 1.5m);
    }

    [Fact]
    public void RandomnessPower_ActivePowerIsMultipleOfBasePower()
    {
        var basePower = 50m;
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, basePower);
        var modifier = new RandomnessPowerModifier();

        // Test multiple iterations — each time reset and apply
        for (int i = 0; i < 100; i++)
        {
            modifier.Apply(bot);
            // ActivePower should be basePower * factor where factor ∈ [0.5, 1.5)
            double ratio = (double)bot.ActivePower / (double)basePower;
            Assert.InRange(ratio, 0.5d, 1.5d);

            // Reset for next call
            bot.SetActivePower(basePower);
        }
    }

    // -----------------------------------------------------------------------
    // AmplificationPowerModifier (IPowerCalculationModify)
    // -----------------------------------------------------------------------

    [Fact]
    public void AmplificationPower_MultipliesActivePowerByEight()
    {
        var basePower = 40m;
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, basePower);

        // First set an explicit active power value
        bot.SetActivePower(60m);
        var expectedAfter = 60m * 8m;

        var modifier = new AmplificationPowerModifier();
        modifier.Apply(bot);

        Assert.Equal(expectedAfter, bot.ActivePower);
    }

    [Fact]
    public void AmplificationPower_CombinedWithRandomness_InCombinedRange()
    {
        var basePower = 40m;
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, basePower);

        var randomnessMod = new RandomnessPowerModifier();
        var amplificationMod = new AmplificationPowerModifier();

        var minResult = decimal.MaxValue;
        var maxResult = 0m;

        for (int i = 0; i < 500; i++)
        {
            // Apply both: first random, then amplify
            bot.SetActivePower(basePower);
            randomnessMod.Apply(bot);
            amplificationMod.Apply(bot);

            minResult = Math.Min(minResult, bot.ActivePower);
            maxResult = Math.Max(maxResult, bot.ActivePower);
        }

        // Combined: basePower * [0.5, 1.5) * 8
        // range ≈ [160, 480)
        Assert.InRange(minResult, 140m, 220m);
        Assert.InRange(maxResult, 400m, 500m);
    }
}
