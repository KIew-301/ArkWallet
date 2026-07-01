using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<TokenPriceChangesData>;

internal class TokenPriceChangeCalculationService(ArkWalletDbContext dbContext, ILogger<TokenPriceChangeCalculationService> logger, TimeProvider timeProvider) : ITokenPriceChangesCalculationService
{
    public async Task<Result<TokenPriceChangesData>> TakeTokenPriceChangesAsync(string symbol, int periodDays)
    {
        try
        {
            if (periodDays < 1)
                return Fail($"Минимальный период для расчёта: 1 день");

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token == null)
                return Fail($"Токен с идентификатором {symbol} не найден");

            var firstCandleInPeriod = await dbContext.PriceCandles
                .FirstOrDefaultAsync(c => c.CharacterTokenId == symbol && c.Timestamp >= timeProvider.GetUtcNow().Date.AddDays(-periodDays));

            if (firstCandleInPeriod == null)
                return Fail("Истории цены токена не существует");

            var currentBalance = token.CurrentPrice;
            var previousBalance = firstCandleInPeriod.OpenPrice;
            var changeAbsolute = currentBalance - previousBalance;
            var сhangePercent = changeAbsolute / previousBalance * 100m;
            return Ok(new TokenPriceChangesData(currentBalance, previousBalance, changeAbsolute, сhangePercent));
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TokenPriceChangeCalculationService:TakeTokenPriceChangesAsync Error");
            return Fail($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}