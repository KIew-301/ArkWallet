using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Tests.Core.General.Application.Services.Orchestrators;

public class PlanModifierCollectionTest
{
    [Fact]
    public void Collections_ArePopulated()
    {
        var coll = new PlanModifierCollection();

        Assert.NotNull(coll.GridModifiers);
        Assert.NotNull(coll.MarketModifiers);
        Assert.NotNull(coll.WallGridModifiers);
        Assert.NotNull(coll.PowerModifiers);
        Assert.NotNull(coll.WallerPowerModifiers);

        Assert.Equal(4, coll.GridModifiers.Count);
        Assert.Equal(4, coll.MarketModifiers.Count);
        Assert.Equal(2, coll.WallGridModifiers.Count);
        Assert.Single(coll.PowerModifiers);
        Assert.Equal(2, coll.WallerPowerModifiers.Count);
    }

    [Fact]
    public void GridModifiers_RequireCurrentPriceAndLevels()
    {
        var coll = new PlanModifierCollection();
        var combined = MarketDataMasks.None;
        foreach (var mod in coll.GridModifiers)
            combined |= mod.RequiredMarketData;

        Assert.True(combined.HasFlag(MarketDataMasks.CurrentPrice));
        Assert.True(combined.HasFlag(MarketDataMasks.ExistingLevels));
    }

    [Fact]
    public void WallerPowerModifiers_IncludeRandomnessAndAmplification()
    {
        var coll = new PlanModifierCollection();

        Assert.NotEmpty(coll.WallerPowerModifiers);
        Assert.NotEmpty(coll.PowerModifiers);
        Assert.Equal("RandomnessPowerModifier", coll.PowerModifiers.First().GetType().Name);

        var hasAmplification = coll.WallerPowerModifiers
            .Any(m => m.GetType().Name == "AmplificationPowerModifier");
        Assert.True(hasAmplification);
    }
}
