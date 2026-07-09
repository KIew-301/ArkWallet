using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Application.Services.CharacterTokenServices;

internal class CandleAggregatorService : ICandleAggregatorService
{
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Task<Result<List<PriceCandleInfo>>> AggregateAsync(List<PriceCandleInfo> candles, int timeframeMinutes)
    {
        try
        {
            if (candles == null || !candles.Any())
                return Task.FromResult(Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>()));

            if (timeframeMinutes <= 0)
                return Task.FromResult(Result<List<PriceCandleInfo>>.Fail("Таймфрейм должен быть больше 0"));

            var grouped = candles
                .GroupBy(c => GetGroupKey(c.DateTime, timeframeMinutes))
                .OrderBy(g => g.Key)
                .Select(g => AggregateGroup(g))
                .ToList();

            return Task.FromResult(Result<List<PriceCandleInfo>>.Ok(grouped));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<List<PriceCandleInfo>>.Fail($"Ошибка агрегации свечей: {ex.Message}"));
        }
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