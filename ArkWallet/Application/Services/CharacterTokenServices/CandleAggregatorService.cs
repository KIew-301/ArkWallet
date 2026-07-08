using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Application.Services.CharacterTokenServices;

internal class CandleAggregatorService : ICandleAggregatorService
{
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
                .Select(g => AggregateGroup(g.ToList()))
                .ToList();

            return Task.FromResult(Result<List<PriceCandleInfo>>.Ok(grouped));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<List<PriceCandleInfo>>.Fail($"Ошибка агрегации свечей: {ex.Message}"));
        }
    }

    private DateTime GetGroupKey(DateTime timestamp, int timeframeMinutes)
    {
        var minutes = (timestamp.Minute / timeframeMinutes) * timeframeMinutes;
        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, minutes, 0);
    }

    private PriceCandleInfo AggregateGroup(List<PriceCandleInfo> group)
    {
        var first = group.First();
        var last = group.Last();

        return new PriceCandleInfo(
            first.OpenPrice,
            group.Max(c => c.HighPrice),
            group.Min(c => c.LowPrice),
            last.ClosePrice,
            first.DateTime,
            first.Timestamp
        );
    }
}