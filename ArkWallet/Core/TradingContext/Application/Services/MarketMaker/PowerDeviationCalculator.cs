using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

namespace ArkWallet.Core.TradingContext.Application.Services.MarketMaker;

/// <summary>Вычисляет коэффициент отклонения мощности для роли MarketMaker на основе исторических свечей.</summary>
public sealed class PowerDeviationCalculator(
    ITokenPriceCandleQueryService candleQueryService)
{
    /// <summary>Асинхронно вычисляет коэффициент отклонения мощности для указанного символа, роли и момента времени.</summary>
    /// <param name="symbol">Тикер инструмента.</param>
    /// <param name="role">Роль market maker (Buyer/Seller).</param>
    /// <param name="utcNow">Текущий момент времени в UTC.</param>
    /// <returns>Коэффициент отклонения мощности в диапазоне [1, 1.25].</returns>
    public async Task<decimal> CalculateAsync(
        string symbol,
        MarketMakerRole role,
        DateTime utcNow)
    {
        var isBuyer = role == MarketMakerRole.Buyer;
        Func<decimal, decimal, decimal> selector = isBuyer ? PowerDeviation.PercentDrop : PowerDeviation.PercentRise;

        var day = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddDays(-1), utcNow, selector);
        var week = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddDays(-7), utcNow, selector);
        var month = await GetClosePriceDeltaPercentAsync(symbol, utcNow.AddMonths(-1), utcNow, selector);

        return PowerDeviation.CalculateCoefficient(day, week, month);
    }

    private async Task<decimal> GetClosePriceDeltaPercentAsync(
        string symbol,
        DateTime start,
        DateTime end,
        Func<decimal, decimal, decimal> selector)
    {
        try
        {
            var candlesResult = await candleQueryService.GetPriceCandlesAsync(symbol, start, end);
            if (!candlesResult.TryGetData(out var candles))
                return 0m;
            if (candles.Count < 2)
                return 0m;
            return selector(candles[0].ClosePrice, candles[candles.Count - 1].ClosePrice);
        }
        catch
        {
            return 0m;
        }
    }
}
