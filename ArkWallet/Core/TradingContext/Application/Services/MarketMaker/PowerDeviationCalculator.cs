using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;

public sealed class PowerDeviationCalculator(
    ITokenPriceCandleQueryService candleQueryService)
{
    public async Task<decimal> CalculateAsync(
        string symbol,
        MarketMakerRole role,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var isBuyer = role == MarketMakerRole.Buyer;
        Func<decimal, decimal, decimal> selector = isBuyer ? PowerDeviation.PercentDrop : PowerDeviation.PercentRise;

        var day = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddDays(-1), utcNow, selector, cancellationToken);
        var week = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddDays(-7), utcNow, selector, cancellationToken);
        var month = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddMonths(-1), utcNow, selector, cancellationToken);

        return PowerDeviation.CalculateCoefficient(day, week, month);
    }

    private async Task<decimal> GetClosePriceDeltaPercentAsync(
        string symbol,
        DateTime start,
        DateTime end,
        Func<decimal, decimal, decimal> selector,
        CancellationToken ct)
    {
        try
        {
            var candlesResult = await candleQueryService.GetPriceCandlesAsync(symbol, start, end);
            if (!candlesResult.TryGetData(out var candles))
                return 0m;
            if (candles.Count < 2)
                return 0m;
            return selector(candles.First().ClosePrice, candles.Last().ClosePrice);
        }
        catch
        {
            return 0m;
        }
    }
}
