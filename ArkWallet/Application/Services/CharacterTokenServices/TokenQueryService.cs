using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<List<TokenInfoWithPriceChange>>;

internal class TokenQueryService(
    ArkWalletDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<TokenQueryService> logger) : ITokenQueryService
{
    public async Task<Result<List<TokenInfoWithPriceChange>>> GetAllActiveTokensAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .AsNoTracking()
                .ToListAsync();

            if (tokens.Count == 0)
                return Ok(new List<TokenInfoWithPriceChange>());

            tokens = tokens.OrderByDescending(t => t.CurrentPrice).ToList();

            var cutoffDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
            var symbols = tokens.Select(t => t.Symbol).ToArray();

            var firstCandleOpenPrices = await dbContext.PriceCandles
                .Where(c => symbols.Contains(c.CharacterTokenId) && c.Timestamp >= cutoffDate)
                .GroupBy(c => c.CharacterTokenId)
                .Select(g => new
                {
                    TokenId = g.Key,
                    OpenPrice = g.OrderBy(c => c.Timestamp).Select(c => c.OpenPrice).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.TokenId, x => x.OpenPrice);

            var result = new List<TokenInfoWithPriceChange>(tokens.Count);

            foreach (var token in tokens)
            {
                var dailyChangePercent = firstCandleOpenPrices.TryGetValue(token.Symbol, out var openPrice)
                    ? (token.CurrentPrice - openPrice) / openPrice * 100m
                    : 0m;

                result.Add(TokenInfoWithPriceChange.FromEntity(token, dailyChangePercent));
            }

            return Ok(result);
        }, logger, nameof(TokenQueryService));
    }

    public async Task<Result<TokenInfo>> GetTokenInfoAsync(string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var token = await dbContext.CharacterTokens
                .FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token == null)
                return Result<TokenInfo>.Fail("Токен не найден");

            return Result<TokenInfo>.Ok(TokenInfo.FromEntity(token));
        }, logger, nameof(TokenQueryService));
    }
}