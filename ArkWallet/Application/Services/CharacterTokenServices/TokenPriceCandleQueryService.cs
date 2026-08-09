using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
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
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (startDateTime >= endDateTime)
                return Fail("Дата начала должна быть меньше даты окончания");

            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Символ токена не может быть пустым");

            var candles = await dbContext.PriceCandles
                .AsNoTracking()
                .Where(c => c.CharacterTokenId == symbol && c.Timestamp >= startDateTime && c.Timestamp < endDateTime)
                .OrderBy(c => c.Timestamp)
                .Select(c => new { c.OpenPrice, c.HighPrice, c.LowPrice, c.ClosePrice, c.Timestamp })
                .ToListAsync();

            if (candles.Count == 0)
                return Ok(new List<PriceCandleInfo>());

            var result = candles
                .Select(c => new PriceCandleInfo(
                    c.OpenPrice,
                    c.HighPrice,
                    c.LowPrice,
                    c.ClosePrice,
                    c.Timestamp,
                    new DateTimeOffset(c.Timestamp).ToUnixTimeSeconds()))
                .ToList();

            return Ok(result);
        }, logger, nameof(TokenPriceCandleQueryService));
    }
}