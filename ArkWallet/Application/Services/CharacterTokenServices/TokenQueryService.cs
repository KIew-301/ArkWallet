using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<List<TokenInfoWithPriceChange>>;

internal class TokenQueryService(
    ArkWalletDbContext dbContext,
    ITokenPriceChangesCalculationService priceChangeService,
    ILogger<TokenQueryService> logger) : ITokenQueryService
{
    public async Task<Result<List<TokenInfoWithPriceChange>>> GetAllActiveTokensAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .ToListAsync();

            var result = new List<TokenInfoWithPriceChange>();

            foreach (var token in tokens)
            {
                var changeResult = await priceChangeService.TakeTokenPriceChangesAsync(token.Symbol, 1);

                var dailyChangePercent = changeResult.TryGetData(out var changeData)
                    ? changeData.ChangePercent
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
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token == null)
                return Result<TokenInfo>.Fail("Токен не найден");

            return Result<TokenInfo>.Ok(TokenInfo.FromEntity(token));
        }, logger, nameof(TokenQueryService));
    }
}