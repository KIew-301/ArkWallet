using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Tests.Core.TradingContext.Domain.MarketMakerAggregate.Modifiers;

public class MarketPlanModifiersTest
{
    private static readonly PlacedOrderLevel[] EmptyLevels = Array.Empty<PlacedOrderLevel>();

    private static MarketConditions Conditions => new(100m, null, null, EmptyLevels);

    private static IReadOnlyCollection<CreateMarketOrderCommand> BuildPlan(string direction, int quantity)
        => new[] { new CreateMarketOrderCommand(1, direction, "TEST", quantity, 100m) };

    // -----------------------------------------------------------------------
    // QuantityCorrectionModifier  →  BasePriceStabilityModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void BasePriceStability_CorrectsQuantity_WhenDeviationExists()
    {
        // BasePrice = 100, CurrentPrice = 150 -> deviation = +50%
        var conditions = new MarketConditions(150m, null, 100m, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        // coefficient = 2 / 1.2 = 1.6667
        // reduce = clamp(50 * 1.6667, 0, 100) = 83.33%
        // newQty = floor(100 * (1 - 0.8333)) = floor(16.67) = 16
        Assert.Single(result);
        Assert.Equal(16, result.First().Quantity);
    }

    [Fact]
    public void BasePriceStability_NoCorrection_WhenNoBasePrice()
    {
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void BasePriceStability_NoCorrection_WhenBasePriceZero()
    {
        var conditions = new MarketConditions(100m, null, 0m, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void BasePriceStability_SellDirection_DeviationHurtsSell()
    {
        // BasePrice = 100, CurrentPrice = 50 -> deviation = -50%
        // direction = "продать" -> adverse = max(-(-50), 0) = 50
        var conditions = new MarketConditions(50m, null, 100m, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Seller, 100m);
        var plan = BuildPlan("продать", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        // reduce = clamp(50 * 1.6667, 0, 100) = 83.33%
        // qty = floor(100 * 0.1667) = 16
        Assert.Single(result);
        Assert.Equal(16, result.First().Quantity);
    }

    // -----------------------------------------------------------------------
    // OrderImpulseModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void OrderImpulse_ManyCalls_ReturnsOriginalPlanMostOfTheTime()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 10);
        var modifier = new OrderImpulseModifier();

        int passthroughCount = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = modifier.Build(bot, Conditions, plan);
            if (ReferenceEquals(result, plan))
                passthroughCount++;
        }

        // 90% chance of factor=1.0 -> expect ~180 passthroughs
        Assert.True(passthroughCount >= 140);
        Assert.True(passthroughCount <= 200);
    }

    [Fact]
    public void OrderImpulse_NonPassthrough_IncreasesQuantity()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var modifier = new OrderImpulseModifier();

        var resultsWithIncreasedQty = new List<int>();
        for (int i = 0; i < 1000; i++)
        {
            var result = modifier.Build(bot, Conditions, BuildPlan("купить", 10));
            if (!ReferenceEquals(result, BuildPlan("купить", 10)))
            {
                var originalPlan = new[] { new CreateMarketOrderCommand(1, "купить", "TEST", 10, 100m) };
                var r = modifier.Build(bot, Conditions, originalPlan);
                if (!ReferenceEquals(r, originalPlan))
                    resultsWithIncreasedQty.Add(r.First().Quantity);
            }
        }

        Assert.NotEmpty(resultsWithIncreasedQty);
        foreach (var qty in resultsWithIncreasedQty)
        {
            Assert.True(qty > 10, $"Expected quantity > 10 but got {qty}");
        }
    }

    // -----------------------------------------------------------------------
    // GridReductionModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void GridReduction_AllQuantitiesScaledBySameFactorRange()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = new[]
        {
            new CreateMarketOrderCommand(1, "купить", "TEST", 10, 100m),
            new CreateMarketOrderCommand(1, "продать", "TEST", 20, 105m),
        };

        var results = new List<(int q1, int q2)>();
        var modifier = new GridReductionModifier();
        for (int i = 0; i < 100; i++)
        {
            var result = modifier.Build(bot, Conditions, plan);
            results.Add((result.ElementAt(0).Quantity, result.ElementAt(1).Quantity));
        }

        // All orders get same factor -> ratio q2/q1 should be constant ≈ 2.0
        foreach (var (q1, q2) in results.Where(r => r.q1 != 0))
        {
            double ratio = (double)q2 / q1;
            Assert.InRange(ratio, 1.8d, 2.2d);
        }
    }

    // -----------------------------------------------------------------------
    // DailyCorrectionModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void DailyCorrection_NullDayAgoPrice_PassThrough()
    {
        var conditions = new MarketConditions(100m, null, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void DailyCorrection_ZeroDayAgoPrice_PassThrough()
    {
        var conditions = new MarketConditions(100m, 0m, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Same(plan, result);
    }

    [Fact]
    public void DailyCorrection_BelowDeadBand_NoReduction()
    {
        // deviation = (105 - 100) / 100 * 100 = 5%, dead band = 10% -> no reduction
        var conditions = new MarketConditions(105m, 100m, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Single(result);
        Assert.Equal(100, result.First().Quantity);
    }

    [Fact]
    public void DailyCorrection_AtFullReduction_OrderDropped()
    {
        // deviation = (150 - 100) / 100 * 100 = 50%, fullReduction = 50% -> 100% reduce
        var conditions = new MarketConditions(150m, 100m, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Empty(result);
    }

    [Fact]
    public void DailyCorrection_PartialReduction_ReducesQuantity()
    {
        // deviation = (130 - 100) / 100 * 100 = 30%
        // span = 50 - 10 = 40, coeff = 100/40 = 2.5
        // beyondDeadBand = max(30 - 10, 0) = 20
        // reduce = min(20 * 2.5, 100) = 50%
        // qty = floor(100 * 0.5) = 50
        var conditions = new MarketConditions(130m, 100m, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Single(result);
        Assert.Equal(50, result.First().Quantity);
    }

    [Fact]
    public void DailyCorrection_BuyDirection_PriceDrop_NoAdverse()
    {
        // deviation = (90 - 100) / 100 * 100 = -10%
        // direction = "купить" -> adverse = max(-10, 0) = 0
        var conditions = new MarketConditions(90m, 100m, null, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new DailyCorrectionModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Single(result);
        Assert.Equal(100, result.First().Quantity);
    }

    // -----------------------------------------------------------------------
    // OrderRandomnessModifier
    // -----------------------------------------------------------------------

    [Fact]
    public void OrderRandomness_QuantityWithinRange_ForOriginalQuantity()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var originalQty = 50;
        var plan = BuildPlan("купить", originalQty);
        var modifier = new OrderRandomnessModifier();

        // Factor ∈ [0.8, 1.2)
        // qty ∈ [floor(50*0.8), floor(50*1.2)) = [40, 60]
        int maxQty = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = modifier.Build(bot, Conditions, BuildPlan("купить", originalQty));
            maxQty = Math.Max(maxQty, result.First().Quantity);
        }

        Assert.InRange(maxQty, 40, 60);
    }

    [Fact]
    public void OrderRandomness_EmptyPlan_PassThrough()
    {
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var modifier = new OrderRandomnessModifier();
        var result = modifier.Build(bot, Conditions, Array.Empty<CreateMarketOrderCommand>());

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // BasePriceStabilityModifier (additional scenarios)
    // -----------------------------------------------------------------------

    [Fact]
    public void BasePriceStability_CurrentEqualsBase_NoCorrection()
    {
        // deviation = 0% -> no reduction
        var conditions = new MarketConditions(100m, null, 100m, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Single(result);
        Assert.Equal(100, result.First().Quantity);
    }

    [Fact]
    public void BasePriceStability_PriceBelowBase_BuyNotAffected()
    {
        // BasePrice = 100, CurrentPrice = 80 -> deviation = -20%
        // direction = "купить" -> adverse = max(-20, 0) = 0
        var conditions = new MarketConditions(80m, null, 100m, EmptyLevels);
        var bot = MarketMakerBot.Create(1, "TEST", MarketMakerRole.Buyer, 100m);
        var plan = BuildPlan("купить", 100);

        var modifier = new BasePriceStabilityModifier();
        var result = modifier.Build(bot, conditions, plan);

        Assert.Single(result);
        Assert.Equal(100, result.First().Quantity);
    }
}
