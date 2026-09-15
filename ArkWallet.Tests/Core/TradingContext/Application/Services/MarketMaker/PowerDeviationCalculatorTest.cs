using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using Moq;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.MarketMaker;

public class PowerDeviationCalculatorTest
{
    private static List<PriceCandleInfo> CreateFallingCandles()
        => new()
        {
            new(105m, 107m, 98m, 100m, DateTime.UtcNow.AddDays(-4), 1000),
            new(98m, 102m, 85m, 80m, DateTime.UtcNow.AddDays(-2), 1200)
        };

    private static List<PriceCandleInfo> CreateRisingCandles()
        => new()
        {
            new(75m, 80m, 70m, 78m, DateTime.UtcNow.AddDays(-4), 900),
            new(80m, 125m, 78m, 120m, DateTime.UtcNow.AddDays(-2), 1100)
        };

    [Fact]
    public async Task CalculateAsync_BuyerWithFallingCandles_ReturnsAboveOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateFallingCandles();

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(candles));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);

        Assert.True(result > 1m);
    }

    [Fact]
    public async Task CalculateAsync_SellerWithRisingCandles_ReturnsAboveOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var candles = CreateRisingCandles();

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(candles));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Seller, utcNow);

        Assert.True(result > 1m);
    }

    [Fact]
    public async Task CalculateAsync_EmptyCandlesList_ReturnsOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>()));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);

        Assert.Equal(1m, result);
    }

    [Fact]
    public async Task CalculateAsync_SingleCandle_ReturnsOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo> {
                new(100m, 105m, 95m, 102m, DateTime.UtcNow, 500)
            }));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);

        Assert.Equal(1m, result);
    }

    [Fact]
    public async Task CalculateAsync_QueryFails_ReturnsOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Fail("query error"));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);

        Assert.Equal(1m, result);
    }

    [Fact]
    public async Task CalculateAsync_ThrowsException_ReturnsOne()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("service down"));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var result = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);

        Assert.Equal(1m, result);
    }

    [Fact]
    public async Task CalculateAsync_RaisesBuyerCoeffWhenFalling_RaisesSellerWhenRising()
    {
        var candleMock = new Mock<ITokenPriceCandleQueryService>();
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var fallingCandles = CreateFallingCandles();
        var risingCandles = CreateRisingCandles();

        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(fallingCandles));

        var calc = new PowerDeviationCalculator(candleMock.Object);

        var buyerResult = await calc.CalculateAsync("ZZZ", MarketMakerRole.Buyer, utcNow);
        candleMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(risingCandles));

        var sellerResult = await calc.CalculateAsync("ZZZ", MarketMakerRole.Seller, utcNow);

        Assert.True(buyerResult > 1m);
        Assert.True(sellerResult > 1m);
    }
}
