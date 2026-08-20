using ArkWallet.Domain.Engines;

namespace ArkWallet.Tests.DomainTests;

public class WallBlockerEngineTest
{
    private static readonly decimal[] TestPrices = [100m, 1000m, 123.45m];

    public static TheoryData<decimal> TestPricesData => new(TestPrices);

    [Theory]
    [MemberData(nameof(TestPricesData))]
    public void GetLevels_ReturnsTenLevels_FiveBuyAndFiveSell(decimal currentPrice)
    {
        var engine = new WallBlockerEngine();

        var levels = engine.GetLevels(currentPrice);

        Assert.Equal(10, levels.Count);
        Assert.Equal(5, levels.Count(l => l.Direction == "купить"));
        Assert.Equal(5, levels.Count(l => l.Direction == "продать"));
    }

    [Theory]
    [MemberData(nameof(TestPricesData))]
    public void GetLevels_BuyLevelsAreBelowPrice_SellLevelsAreAbove(decimal currentPrice)
    {
        var engine = new WallBlockerEngine();

        var levels = engine.GetLevels(currentPrice);

        foreach (var level in levels.Where(l => l.Direction == "купить"))
            Assert.True(level.Price < currentPrice, $"Buy level {level.Price} must be below {currentPrice}");

        foreach (var level in levels.Where(l => l.Direction == "продать"))
            Assert.True(level.Price > currentPrice, $"Sell level {level.Price} must be above {currentPrice}");
    }

    [Theory]
    [MemberData(nameof(TestPricesData))]
    public void GetLevels_NearestLevelsAreWithin_3To5Percent_OfPrice(decimal currentPrice)
    {
        var engine = new WallBlockerEngine();

        var levels = engine.GetLevels(currentPrice);

        var nearestBuy = levels.Where(l => l.Direction == "купить").OrderByDescending(l => l.Price).First();
        var nearestSell = levels.Where(l => l.Direction == "продать").OrderBy(l => l.Price).First();

        var buyOffset = (currentPrice - nearestBuy.Price) / currentPrice;
        var sellOffset = (nearestSell.Price - currentPrice) / currentPrice;

        Assert.InRange(buyOffset, 0.03m, 0.05m);
        Assert.InRange(sellOffset, 0.03m, 0.05m);
    }

    [Theory]
    [MemberData(nameof(TestPricesData))]
    public void GetLevels_AdjacentLevelsAreWithin_2To10Percent_OfPrice(decimal currentPrice)
    {
        var engine = new WallBlockerEngine();

        var levels = engine.GetLevels(currentPrice);

        foreach (var side in new[] { "купить", "продать" })
        {
            var sideLevels = levels
                .Where(l => l.Direction == side)
                .OrderByDescending(l => l.Price)
                .ToArray();

            for (int i = 1; i < sideLevels.Length; i++)
            {
                var gap = Math.Abs(sideLevels[i - 1].Price - sideLevels[i].Price) / currentPrice;
                Assert.InRange(gap, 0.02m, 0.10m);
            }
        }
    }

    [Fact]
    public void GetLevels_WhenPriceIsNonPositive_ReturnsEmpty()
    {
        var engine = new WallBlockerEngine();

        Assert.Empty(engine.GetLevels(0));
        Assert.Empty(engine.GetLevels(-50));
    }
}
