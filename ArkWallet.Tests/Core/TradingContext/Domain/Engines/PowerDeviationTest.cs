using ArkWallet.Core.TradingContext.Domain.Engines;

namespace ArkWallet.Tests.Core.TradingContext.Domain.Engines;

public class PowerDeviationTest
{
    [Fact]
    public void CalculateCoefficient_NoDrop_ReturnsOne()
    {
        var result = PowerDeviation.CalculateCoefficient(0m, 0m, 0m);

        Assert.Equal(1m, result);
    }

    [Fact]
    public void CalculateCoefficient_DayDrop_ContributesProportionally()
    {
        var result = PowerDeviation.CalculateCoefficient(2m, 0m, 0m);

        Assert.Equal(1.005m, result);
    }

    [Fact]
    public void CalculateCoefficient_WeekDrop_ContributesProportionally()
    {
        var result = PowerDeviation.CalculateCoefficient(0m, 3m, 0m);

        Assert.Equal(1.005m, result);
    }

    [Fact]
    public void CalculateCoefficient_MonthDrop_ContributesProportionally()
    {
        var result = PowerDeviation.CalculateCoefficient(0m, 0m, 4.5m);

        Assert.Equal(1.005m, result);
    }

    [Fact]
    public void CalculateCoefficient_AllPeriodsDropped_SumsContributions()
    {
        var result = PowerDeviation.CalculateCoefficient(4m, 6m, 9m);

        Assert.Equal(1.03m, result);
    }

    [Fact]
    public void CalculateCoefficient_PartialDrops_SumsContributions()
    {
        var result = PowerDeviation.CalculateCoefficient(4m, 3m, 4.5m);

        Assert.Equal(1.02m, result);
    }

    [Fact]
    public void CalculateCoefficient_LargeDrops_ClampsToMax()
    {
        var result = PowerDeviation.CalculateCoefficient(50m, 50m, 50m);

        Assert.Equal(1.25m, result);
    }

    [Fact]
    public void CalculateCoefficient_ModerateDrops_StaysUnclamped()
    {
        var result = PowerDeviation.CalculateCoefficient(8m, 6m, 9m);

        Assert.Equal(1.04m, result);
    }

    [Fact]
    public void PercentDrop_PositiveOnFall()
    {
        var result = PowerDeviation.PercentDrop(100m, 80m);

        Assert.Equal(20m, result);
    }

    [Fact]
    public void PercentDrop_HalfPrice_TwoThirdsLoss()
    {
        var result = PowerDeviation.PercentDrop(1000m, 100m);

        Assert.Equal(90m, result);
    }

    [Fact]
    public void PercentDrop_ZeroWhenFirstIsZero()
    {
        var result = PowerDeviation.PercentDrop(0m, 100m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void PercentRise_PositiveOnRise()
    {
        var result = PowerDeviation.PercentRise(100m, 120m);

        Assert.Equal(20m, result);
    }

    [Fact]
    public void PercentRise_DoublePrice_ReturnsHundred()
    {
        var result = PowerDeviation.PercentRise(0.5m, 1m);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void PercentRise_ZeroWhenFirstIsZero()
    {
        var result = PowerDeviation.PercentRise(0m, 100m);

        Assert.Equal(0m, result);
    }
}