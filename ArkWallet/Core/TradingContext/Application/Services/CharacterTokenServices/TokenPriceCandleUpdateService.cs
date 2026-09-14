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

            if (lastCandle == null)
            {
                var newCandle = PriceCandle.Create(symbol, newPrice, dateTimeNow);
                await dbContext.PriceCandles.AddAsync(newCandle);
            }
            else if (lastCandle.Timestamp.AddMinutes(SavingTimeFrameInMinute) <= dateTimeNow)
            {
                var newCandle = PriceCandle.Create(symbol, lastCandle.ClosePrice, dateTimeNow);
                newCandle.ClosePrice = newPrice;
                if (newPrice > newCandle.HighPrice)
                    newCandle.HighPrice = newPrice;
                if (newPrice < newCandle.LowPrice)
                    newCandle.LowPrice = newPrice;
                await dbContext.PriceCandles.AddAsync(newCandle);
            }
            else
            {
                lastCandle.ClosePrice = newPrice;
                if (newPrice > lastCandle.HighPrice)
                    lastCandle.HighPrice = newPrice;
                if (newPrice < lastCandle.LowPrice)
                    lastCandle.LowPrice = newPrice;
            }

            return Ok();
        }, logger, nameof(TokenPriceCandleUpdateService));
    }
}
