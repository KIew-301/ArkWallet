using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;

using static Result<List<PriceCandleInfo>>;

internal class TokenPriceCandleQueryService(
    ArkWalletDbContext dbContext,
    ILogger<TokenPriceCandleQueryService> logger) : ITokenPriceCandleQueryService
{
    public async Task<Result<List<PriceCandleInfo>>> GetPriceCandlesAsync(
        string symbol,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        try
        {
            if (startDateTime >= endDateTime)
                return Fail("Дата начала должна быть меньше даты окончания");

            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Символ токена не может быть пустым");

            var candles = await dbContext.PriceCandles
                .Where(c => c.CharacterTokenId == symbol && c.Timestamp >= startDateTime && c.Timestamp < endDateTime)
                .OrderBy(c => c.Timestamp)
                .ToListAsync();

            if (!candles.Any())
                return Ok(new List<PriceCandleInfo>());

            var result = candles
                .Select(PriceCandleInfo.FromEntity)
                .ToList();

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TokenPriceCandleQueryService:GetPriceCandlesAsync Error");
            return Fail($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}