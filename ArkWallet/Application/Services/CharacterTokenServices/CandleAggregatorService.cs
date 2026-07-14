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

            var grouped = candles
                .GroupBy(c => GetGroupKey(c.DateTime, timeframeMinutes))
                .OrderBy(g => g.Key)
                .Select(g => AggregateGroup(g))
                .ToList();

            return Result<List<PriceCandleInfo>>.Ok(grouped);
        }, logger, nameof(CandleAggregatorService));
    }

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

    private static PriceCandleInfo AggregateGroup(IGrouping<DateTime, PriceCandleInfo> group)
    {
        var candles = group.ToList();
        var first = candles[0];
        var last = candles[^1];
        var epochTimestamp = new DateTimeOffset(group.Key, TimeSpan.Zero).ToUnixTimeSeconds();

        return new PriceCandleInfo(
            first.OpenPrice,
            candles.Max(c => c.HighPrice),
            candles.Min(c => c.LowPrice),
            last.ClosePrice,
            group.Key,
            epochTimestamp
        );
    }
}