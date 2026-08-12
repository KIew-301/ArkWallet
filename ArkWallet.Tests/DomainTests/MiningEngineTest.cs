using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Tests.DomainTests;

public class MiningEngineTest
{
    private readonly MiningEngine _engine = new();

    [Fact]
    public void CalculateCash_MultipliesAllCoefficients()
    {
        var result = _engine.CalculateCash(4m, 2m, 3m, 2m);

        Assert.Equal(48m, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5.5)]
    public void CalculateCash_TimingCoeffNotPositive_Throws(decimal timingCoeff)
    {
        var exception = Assert.Throws<DomainException>(() => _engine.CalculateCash(1m, 1m, timingCoeff, 1m));
        Assert.Contains("больше нуля", exception.Message);
    }

    [Fact]
    public void CalculateMiningSpeed_MultipliesCoefficients()
    {
        var result = _engine.CalculateMiningSpeed(4m, 2m, 5m);

        Assert.Equal(40m, result);
    }

    [Fact]
    public void CalculateProfit_MultipliesSpeedByPrice()
    {
        var result = _engine.CalculateProfit(10m, 25m);

        Assert.Equal(250m, result);
    }

    [Fact]
    public void CalculateBaseProfit_MultipliesBaseSpeedByPrice()
    {
        var result = _engine.CalculateBaseProfit(2m, 100m);

        Assert.Equal(200m, result);
    }

    [Fact]
    public void CalculateBaseMiningSpeed_DividesConstantByPrice()
    {
        var result = _engine.CalculateBaseMiningSpeed(50m);

        Assert.Equal(1m, result);
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(100, 0.5)]
    [InlineData(200, 0.25)]
    public void CalculateBaseMiningSpeed_DecreasesAsPriceGrows(int price, decimal expectedSpeed)
    {
        var result = _engine.CalculateBaseMiningSpeed(price);

        Assert.Equal(expectedSpeed, result);
    }

    [Fact]
    public void CollectWholeTokens_ReturnsWholePart()
    {
        Assert.Equal(10, _engine.CollectWholeTokens(10.45m));
    }

    [Fact]
    public void CollectWholeTokens_ZeroFraction_ReturnsWholeNumber()
    {
        Assert.Equal(7, _engine.CollectWholeTokens(7m));
    }

    [Theory]
    [InlineData(0.4, MiningStatus.Unprofitable)]
    [InlineData(0.49, MiningStatus.Unprofitable)]
    [InlineData(0.5, MiningStatus.Stable)]
    [InlineData(0.6, MiningStatus.Stable)]
    [InlineData(0.75, MiningStatus.Stable)]
    [InlineData(0.76, MiningStatus.Profitable)]
    [InlineData(1, MiningStatus.Profitable)]
    public void CalculateStatus_ClassifiesByPosition(decimal value, MiningStatus expected)
    {
        var result = _engine.CalculateStatus(value, 0m, 1m);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculatePosition_ZeroRange_ReturnsMidpoint()
    {
        var result = _engine.CalculatePosition(5m, 5m, 5m);

        Assert.Equal(0.5m, result);
    }

    [Fact]
    public void CalculatePosition_WithinRange_ReturnsRelativePosition()
    {
        var result = _engine.CalculatePosition(60m, 20m, 100m);

        Assert.Equal(0.5m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_NullDates_Returns100()
    {
        var result = _engine.CalculateSwitchingPercent(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, null);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_EndNotAfterStart_Returns100()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.CalculateSwitchingPercent(start, start, end);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_NowAfterEnd_Returns100()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(10);
        var now = start.AddMinutes(15);

        var result = _engine.CalculateSwitchingPercent(now, start, end);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_NowBeforeStart_Returns0()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(10);
        var now = start.AddMinutes(-5);

        var result = _engine.CalculateSwitchingPercent(now, start, end);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_MidSwitch_ReturnsHalf()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(10);
        var now = start.AddMinutes(5);

        var result = _engine.CalculateSwitchingPercent(now, start, end);

        Assert.Equal(50m, result);
    }

    [Fact]
    public void CalculateSwitchingPercent_PartialMinutes_ReturnsFraction()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(10);
        var now = start.AddSeconds(75);

        var result = _engine.CalculateSwitchingPercent(now, start, end);

        Assert.Equal(12.5m, result);
    }

    [Fact]
    public void NextCoefficient_StaysInRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var coefficient = _engine.NextCoefficient();

            Assert.InRange(coefficient, MiningEngine.MinCoefficient, MiningEngine.MaxCoefficient);
        }
    }

    [Fact]
    public void CalculateTimingCoeff_ElapsedMinutes_RoundedToTwoDecimals()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMinutes(2).AddSeconds(20);

        var result = _engine.CalculateTimingCoeff(now, last);

        Assert.Equal(2.33m, result);
    }

    [Fact]
    public void CalculateTimingCoeff_NoElapsedTime_Returns1()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.CalculateTimingCoeff(last, last);

        Assert.Equal(1m, result);
    }

    [Fact]
    public void CalculateTimingCoeff_NowBeforeLast_Returns1()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMinutes(-5);

        var result = _engine.CalculateTimingCoeff(now, last);

        Assert.Equal(1m, result);
    }
}
