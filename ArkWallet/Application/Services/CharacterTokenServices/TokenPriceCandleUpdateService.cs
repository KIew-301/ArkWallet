using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;

internal class TokenPriceCandleUpdateService(ArkWalletDbContext dbContext, TimeProvider timeProvider, ILogger<TokenPriceCandleUpdateService> logger)
{
    public async Task<TokenPriceCandleUpdateResult> UpdateTokenPriceCandleAsync(string symbol, decimal newPrice)
    {
        const int SavingTimeFrameInMinute = 1;

        try
        {
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(c => c.Symbol == symbol);
            if (token == null)
                return TokenPriceCandleUpdateResult.Fail("Токен не найден");

            var lastCandle = await dbContext.PriceCandles
                .Where(c => c.CharacterTokenId == symbol)
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefaultAsync();

            var dateTimeNow = timeProvider.GetUtcNow().UtcDateTime;

            if (lastCandle == null)
            {
                var newCandle = PriceCandle.CreateNew(symbol, newPrice, dateTimeNow);
                await dbContext.PriceCandles.AddAsync(newCandle);
            }
            else if (lastCandle.Timestamp.AddMinutes(SavingTimeFrameInMinute) <= dateTimeNow)
            {
                var newCandle = PriceCandle.CreateNew(symbol, lastCandle.ClosePrice, dateTimeNow);
                newCandle.Update(newPrice);
                await dbContext.PriceCandles.AddAsync(newCandle);
            }
            else
            {
                lastCandle.Update(newPrice);
                dbContext.PriceCandles.Update(lastCandle);
            }

            await dbContext.SaveChangesAsync();
            return TokenPriceCandleUpdateResult.Ok();
        }
        catch (DomainException ex)
        {
            return TokenPriceCandleUpdateResult.Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка сохранения баланса в истории");
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return TokenPriceCandleUpdateResult.Fail($"Внутренняя ошибка сервера: {innerMessage}");
        }
    }
}

internal record TokenPriceCandleUpdateResult(
    bool IsSuccess, string message)
{
    public static TokenPriceCandleUpdateResult Ok()
    {
        return new(true, "Данные цене сохранены в историю");
    }

    public static TokenPriceCandleUpdateResult Fail(string message)
    {
        return new(false, message);
    }
};
