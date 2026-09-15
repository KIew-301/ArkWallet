using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using static Result;

internal class TokenPriceCandleUpdateService(
    ArkWalletDbContext dbContext, TimeProvider timeProvider, 
    ILogger<TokenPriceCandleUpdateService> logger) : ITokenPriceCandleUpdateService
{
    public async Task<Result> UpdateTokenPriceCandleAsync(string symbol, decimal newPrice)
    {
        const int SavingTimeFrameInMinute = 1;

        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var token = await dbContext.CharacterTokens.FindAsync(symbol);
            if (token == null)
                return Fail("Токен не найден");

            var lastCandle = await dbContext.PriceCandles
                .Where(c => c.CharacterTokenId == symbol)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync();

            var dateTimeNow = timeProvider.GetUtcNow().UtcDateTime;

            await UpsertCandleAsync(symbol, lastCandle, dateTimeNow, newPrice, SavingTimeFrameInMinute);

            return Ok();
        }, logger, nameof(TokenPriceCandleUpdateService));
    }

    private async Task UpsertCandleAsync(
        string symbol,
        PriceCandle? lastCandle,
        DateTime dateTimeNow,
        decimal newPrice,
        int savingTimeFrameInMinute)
    {
        if (lastCandle == null)
        {
            var newCandle = PriceCandle.Create(symbol, newPrice, dateTimeNow);
            await dbContext.PriceCandles.AddAsync(newCandle);
        }
        else if (lastCandle.Timestamp.AddMinutes(savingTimeFrameInMinute) <= dateTimeNow)
        {
            var newCandle = PriceCandle.Create(symbol, lastCandle.ClosePrice, dateTimeNow);
            ApplyPrice(newCandle, newPrice);
            await dbContext.PriceCandles.AddAsync(newCandle);
        }
        else
        {
            ApplyPrice(lastCandle, newPrice);
        }
    }

    private static void ApplyPrice(PriceCandle candle, decimal newPrice)
    {
        candle.ClosePrice = newPrice;
        if (newPrice > candle.HighPrice)
            candle.HighPrice = newPrice;
        if (newPrice < candle.LowPrice)
            candle.LowPrice = newPrice;
    }
}
