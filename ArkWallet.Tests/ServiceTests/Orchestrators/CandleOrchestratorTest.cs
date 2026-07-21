using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.Orchestrators;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Orchestrators;

public class CandleOrchestratorTest
{
    private readonly Mock<ITokenPriceCandleQueryService> _candleQueryServiceMock = new();
    private readonly Mock<ICandleAggregatorService> _candleAggregatorServiceMock = new();
    private readonly CandleOrchestrator _orchestrator;

    public CandleOrchestratorTest()
    {
        _orchestrator = new CandleOrchestrator(
            _candleQueryServiceMock.Object,
            _candleAggregatorServiceMock.Object,
            NullLogger<CandleOrchestrator>.Instance);
    }

    [Fact]
    public async Task GetAggregatedCandles_AllMocksReturnSuccess_ReturnsSuccess()
    {
        var candles = new List<PriceCandleInfo>
        {
            new(100m, 110m, 90m, 105m, DateTime.UtcNow, 1000),
            new(105m, 115m, 95m, 110m, DateTime.UtcNow.AddMinutes(1), 1060)
        };

        _candleQueryServiceMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(candles));

        var aggregated = new List<PriceCandleInfo>
        {
            new(100m, 115m, 90m, 110m, DateTime.UtcNow, 1000)
        };

        _candleAggregatorServiceMock
            .Setup(x => x.AggregateAsync(candles, 5))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(aggregated));

        var result = await _orchestrator.GetAggregatedCandlesAsync("ZZZ", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 5);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal(100m, data[0].OpenPrice);
        Assert.Equal(110m, data[0].ClosePrice);
    }

    [Fact]
    public async Task GetAggregatedCandles_CandleQueryServiceFails_ReturnsFail()
    {
        _candleQueryServiceMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Fail("Ошибка получения свечей"));

        var result = await _orchestrator.GetAggregatedCandlesAsync("ZZZ", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 5);

        Assert.False(result.IsSuccess);
        Assert.Contains("Ошибка получения свечей", result.Message);
        _candleAggregatorServiceMock.Verify(
            x => x.AggregateAsync(It.IsAny<List<PriceCandleInfo>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAggregatedCandles_CandleAggregatorServiceFails_ReturnsFail()
    {
        var candles = new List<PriceCandleInfo>
        {
            new(100m, 110m, 90m, 105m, DateTime.UtcNow, 1000)
        };

        _candleQueryServiceMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(candles));

        _candleAggregatorServiceMock
            .Setup(x => x.AggregateAsync(candles, 5))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Fail("Ошибка агрегации свечей"));

        var result = await _orchestrator.GetAggregatedCandlesAsync("ZZZ", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 5);

        Assert.False(result.IsSuccess);
        Assert.Contains("Ошибка агрегации свечей", result.Message);
    }

    [Fact]
    public async Task GetAggregatedCandles_TimeframeIsOne_Minute_ReturnsSuccessWithoutAggregation()
    {
        var candles = new List<PriceCandleInfo>
        {
            new(100m, 110m, 90m, 105m, DateTime.UtcNow, 1000)
        };

        _candleQueryServiceMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(candles));

        var result = await _orchestrator.GetAggregatedCandlesAsync("ZZZ", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 1);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal(100m, data[0].OpenPrice);
        _candleAggregatorServiceMock.Verify(
            x => x.AggregateAsync(It.IsAny<List<PriceCandleInfo>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAggregatedCandles_NoCandles_ReturnsSuccessEmptyList()
    {
        _candleQueryServiceMock
            .Setup(x => x.GetPriceCandlesAsync("ZZZ", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>()));

        var result = await _orchestrator.GetAggregatedCandlesAsync("ZZZ", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 5);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
        _candleAggregatorServiceMock.Verify(
            x => x.AggregateAsync(It.IsAny<List<PriceCandleInfo>>(), It.IsAny<int>()), Times.Never);
    }
}
