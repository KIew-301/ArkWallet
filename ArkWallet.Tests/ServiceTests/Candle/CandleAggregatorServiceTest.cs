using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Candle;

public class CandleAggregatorServiceTest
{
    private readonly CandleAggregatorService _service = new CandleAggregatorService(NullLogger<CandleAggregatorService>.Instance);

    [Fact]
    public async Task AggregateAsync_WithNullCandles_ReturnsEmptyList()
    {
        var result = await _service.AggregateAsync(null!, 5);

        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task AggregateAsync_WithEmptyCandles_ReturnsEmptyList()
    {
        var result = await _service.AggregateAsync(new List<PriceCandleInfo>(), 5);

        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task AggregateAsync_WithInvalidTimeframe_ReturnsFail()
    {
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, DateTime.UtcNow, 0)
        };

        var result = await _service.AggregateAsync(candles, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Таймфрейм должен быть больше 0", result.Message);
    }

    [Fact]
    public async Task AggregateAsync_WithSingleCandle_ReturnsSameCandle()
    {
        var baseTime = new DateTime(2026, 7, 9, 14, 3, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
{
    new(100, 102, 99, 101, baseTime, 0)
};

        var result = await _service.AggregateAsync(candles, 5);

        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);

        var candle = data.First();
        Assert.Equal(100, candle.OpenPrice);
        Assert.Equal(102, candle.HighPrice);
        Assert.Equal(99, candle.LowPrice);
        Assert.Equal(101, candle.ClosePrice);

        var expectedDateTime = new DateTime(2026, 7, 9, 14, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDateTime, candle.DateTime);
    }

    [Fact]
    public async Task AggregateAsync_WithFiveMinuteCandles_ReturnsCorrectAggregation()
    {
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddMinutes(1), 0),
            new(102, 104, 101, 103, baseTime.AddMinutes(2), 0),
            new(103, 105, 102, 104, baseTime.AddMinutes(3), 0),
            new(104, 106, 103, 105, baseTime.AddMinutes(4), 0)
        };

        var result = await _service.AggregateAsync(candles, 5);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);

        var candle = data.First();
        Assert.Equal(100, candle.OpenPrice);
        Assert.Equal(106, candle.HighPrice);
        Assert.Equal(99, candle.LowPrice);
        Assert.Equal(105, candle.ClosePrice);
        Assert.Equal(baseTime, candle.DateTime);
    }

    [Fact]
    public async Task AggregateAsync_WithMultipleGroups_ReturnsCorrectAggregation()
    {
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddMinutes(1), 0),
            new(102, 104, 101, 103, baseTime.AddMinutes(2), 0),
            new(103, 105, 102, 104, baseTime.AddMinutes(3), 0),
            new(104, 106, 103, 105, baseTime.AddMinutes(4), 0),
            new(105, 107, 104, 106, baseTime.AddMinutes(5), 0),
            new(106, 108, 105, 107, baseTime.AddMinutes(6), 0),
            new(107, 109, 106, 108, baseTime.AddMinutes(7), 0),
            new(108, 110, 107, 109, baseTime.AddMinutes(8), 0),
            new(109, 111, 108, 110, baseTime.AddMinutes(9), 0)
        };

        var result = await _service.AggregateAsync(candles, 5);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(106, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(105, data[0].ClosePrice);
        Assert.Equal(baseTime, data[0].DateTime);

        Assert.Equal(105, data[1].OpenPrice);
        Assert.Equal(111, data[1].HighPrice);
        Assert.Equal(104, data[1].LowPrice);
        Assert.Equal(110, data[1].ClosePrice);
        Assert.Equal(baseTime.AddMinutes(5), data[1].DateTime);
    }

    [Fact]
    public async Task AggregateAsync_WithPartialGroup_ReturnsPartialGroup()
    {
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddMinutes(1), 0),
            new(102, 104, 101, 103, baseTime.AddMinutes(2), 0)
        };

        var result = await _service.AggregateAsync(candles, 5);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(104, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(103, data[0].ClosePrice);
    }

    [Fact]
    public async Task AggregateAsync_WithDifferentTimeframes_ReturnsCorrectAggregation()
    {
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddMinutes(1), 0),
            new(102, 104, 101, 103, baseTime.AddMinutes(2), 0),
            new(103, 105, 102, 104, baseTime.AddMinutes(3), 0)
        };

        var result10 = await _service.AggregateAsync(candles, 10);

        Assert.True(result10.IsSuccess);
        Assert.True(result10.TryGetData(out var data10));
        Assert.Single(data10);

        Assert.Equal(100, data10[0].OpenPrice);
        Assert.Equal(105, data10[0].HighPrice);
        Assert.Equal(99, data10[0].LowPrice);
        Assert.Equal(104, data10[0].ClosePrice);

        var result2 = await _service.AggregateAsync(candles, 2);

        Assert.True(result2.IsSuccess);
        Assert.True(result2.TryGetData(out var data2));
        Assert.Equal(2, data2.Count);

        Assert.Equal(100, data2[0].OpenPrice);
        Assert.Equal(103, data2[0].HighPrice);
        Assert.Equal(99, data2[0].LowPrice);
        Assert.Equal(102, data2[0].ClosePrice);

        Assert.Equal(102, data2[1].OpenPrice);
        Assert.Equal(105, data2[1].HighPrice);
        Assert.Equal(101, data2[1].LowPrice);
        Assert.Equal(104, data2[1].ClosePrice);
    }

    [Fact]
    public async Task AggregateAsync_HourlyTimeframe_GroupsByHours()
    {
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddMinutes(15), 0),
            new(105, 110, 104, 108, baseTime.AddHours(1), 0),
            new(108, 112, 107, 110, baseTime.AddHours(1).AddMinutes(30), 0)
        };

        var result = await _service.AggregateAsync(candles, 60);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(103, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(102, data[0].ClosePrice);

        Assert.Equal(105, data[1].OpenPrice);
        Assert.Equal(112, data[1].HighPrice);
        Assert.Equal(104, data[1].LowPrice);
        Assert.Equal(110, data[1].ClosePrice);
    }

    [Fact]
    public async Task AggregateAsync_TwoHourTimeframe_GroupsByTwoHours()
    {
        var baseTime = new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(103, 105, 102, 104, baseTime.AddHours(1), 0),
            new(107, 110, 106, 109, baseTime.AddHours(2), 0),
            new(109, 111, 108, 110, baseTime.AddHours(3), 0)
        };

        var result = await _service.AggregateAsync(candles, 120);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(105, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(104, data[0].ClosePrice);

        Assert.Equal(107, data[1].OpenPrice);
        Assert.Equal(111, data[1].HighPrice);
        Assert.Equal(106, data[1].LowPrice);
        Assert.Equal(110, data[1].ClosePrice);
    }

    [Fact]
    public async Task AggregateAsync_DailyTimeframe_GroupsByDays()
    {
        var baseTime = new DateTime(2026, 7, 8, 14, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, baseTime, 0),
            new(101, 103, 100, 102, baseTime.AddHours(6), 0),
            new(105, 110, 104, 108, baseTime.AddDays(1), 0),
            new(108, 112, 107, 110, baseTime.AddDays(1).AddHours(4), 0)
        };

        var result = await _service.AggregateAsync(candles, 1440);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(103, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(102, data[0].ClosePrice);

        Assert.Equal(105, data[1].OpenPrice);
        Assert.Equal(112, data[1].HighPrice);
        Assert.Equal(104, data[1].LowPrice);
        Assert.Equal(110, data[1].ClosePrice);
    }

    [Fact]
    public async Task AggregateAsync_WeeklyTimeframe_GroupsByWeeks()
    {
        var day0 = new DateTime(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);
        var candles = new List<PriceCandleInfo>
        {
            new(100, 102, 99, 101, day0, 0),
            new(101, 103, 100, 102, day0.AddDays(2), 0),
            new(110, 115, 109, 113, day0.AddDays(10), 0),
            new(113, 116, 112, 114, day0.AddDays(12), 0)
        };

        var result = await _service.AggregateAsync(candles, 10080);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        Assert.Equal(100, data[0].OpenPrice);
        Assert.Equal(103, data[0].HighPrice);
        Assert.Equal(99, data[0].LowPrice);
        Assert.Equal(102, data[0].ClosePrice);

        Assert.Equal(110, data[1].OpenPrice);
        Assert.Equal(116, data[1].HighPrice);
        Assert.Equal(109, data[1].LowPrice);
        Assert.Equal(114, data[1].ClosePrice);
    }
}