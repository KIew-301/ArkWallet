using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Orchestrators;

namespace ArkWallet.Application.Services.Orchestrators;

internal class CandleOrchestrator(
    ITokenPriceCandleQueryService candleQueryService,
    ICandleAggregatorService candleAggregatorService,
    ILogger<CandleOrchestrator> logger) : ICandleOrchestrator
{
    public async Task<Result<List<PriceCandleInfo>>> GetAggregatedCandlesAsync(
        string symbol,
        DateTime startDateTime,
        DateTime endDateTime,
        int timeframeMinutes)
    {
        try
        {
            // 1. Получаем свечи из сервиса
            var candlesResult = await candleQueryService.GetPriceCandlesAsync(
                symbol,
                startDateTime,
                endDateTime);

            if (!candlesResult.TryGetData(out var candles))
                return Result<List<PriceCandleInfo>>.Fail(candlesResult.Message);

            if (!candles.Any())
                return Result<List<PriceCandleInfo>>.Ok(new List<PriceCandleInfo>());

            if (timeframeMinutes == 1)
                return Result<List<PriceCandleInfo>>.Ok(candles);

            var aggregatedResult = await candleAggregatorService.AggregateAsync(candles, timeframeMinutes);

            if (!aggregatedResult.TryGetData(out var aggregated))
                return Result<List<PriceCandleInfo>>.Fail(aggregatedResult.Message);

            return Result<List<PriceCandleInfo>>.Ok(aggregated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CandleOrchestrator:GetAggregatedCandlesAsync Error");
            return Result<List<PriceCandleInfo>>.Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }
}