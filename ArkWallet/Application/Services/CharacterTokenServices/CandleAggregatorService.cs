using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;

internal class CandleAggregatorService(ILogger<CandleAggregatorService> logger) : ICandleAggregatorService
{
    private static readonly DateTime Epoch = DateTime.UnixEpoch;

    public async Task<Result<List<PriceCandleInfo>>> AggregateAsync(List<PriceCandleInfo> candles, int timeframeMinutes)
    {
        return await ServiceErrorHandler.ExecuteAsync<List<PriceCandleInfo>>(async () =>
        {
            if (candles is null || candles.Count == 0)
                return Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>());

            if (timeframeMinutes <= 0)
                return Result<List<PriceCandleInfo>>.Fail("Таймфрейм должен быть больше 0");

            return Result<List<PriceCandleInfo>>.Ok(AggregateGroups(candles, timeframeMinutes));
        }, logger, nameof(CandleAggregatorService));
    }

    private static List<PriceCandleInfo> AggregateGroups(List<PriceCandleInfo> candles, int timeframeMinutes)
    {
        var result = new List<PriceCandleInfo>();
        var currentKey = GetGroupKey(candles[0].DateTime, timeframeMinutes);
        var open = candles[0].OpenPrice;
        var high = candles[0].HighPrice;
        var low = candles[0].LowPrice;
        var close = candles[0].ClosePrice;

        for (var i = 1; i < candles.Count; i++)
        {
            var candle = candles[i];
            var key = GetGroupKey(candle.DateTime, timeframeMinutes);

            if (key != currentKey)
            {
                result.Add(CreateCandle(currentKey, open, high, low, close));
                currentKey = key;
                open = candle.OpenPrice;
                high = candle.HighPrice;
                low = candle.LowPrice;
                close = candle.ClosePrice;
            }
            else
            {
                MergeIntoGroup(ref high, ref low, ref close, candle);
            }
        }

        result.Add(CreateCandle(currentKey, open, high, low, close));
        return result;
    }

    private static void MergeIntoGroup(ref decimal high, ref decimal low, ref decimal close, PriceCandleInfo candle)
    {
        if (candle.HighPrice > high) high = candle.HighPrice;
        if (candle.LowPrice < low) low = candle.LowPrice;
        close = candle.ClosePrice;
    }

    private static PriceCandleInfo CreateCandle(DateTime key, decimal open, decimal high, decimal low, decimal close)
        => new(open, high, low, close, key, new DateTimeOffset(key, TimeSpan.Zero).ToUnixTimeSeconds());

    private static DateTime GetGroupKey(DateTime timestamp, int timeframeMinutes)
    {
        if (timeframeMinutes >= 1440)
        {
            var days = timeframeMinutes / 1440;
            var totalDays = (timestamp.Date - Epoch).Days;
            var groupedDays = (totalDays / days) * days;
            return Epoch.AddDays(groupedDays);
        }

        if (timeframeMinutes >= 60)
        {
            var hoursBlock = timeframeMinutes / 60;
            var totalHours = (timestamp.Date - Epoch).Days * 24 + timestamp.Hour;
            var groupedHours = (totalHours / hoursBlock) * hoursBlock;
            return Epoch.AddHours(groupedHours);
        }

        var totalMinutes = (int)(timestamp - timestamp.Date).TotalMinutes;
        var groupedMinutes = (totalMinutes / timeframeMinutes) * timeframeMinutes;
        return timestamp.Date.AddMinutes(groupedMinutes);
    }
}