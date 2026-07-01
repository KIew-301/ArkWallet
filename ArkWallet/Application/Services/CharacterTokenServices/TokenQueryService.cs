using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result<List<TokenInfo>>;

internal class TokenQueryService(
    ArkWalletDbContext dbContext,
    ITokenPriceChangesCalculationService priceChangeService,
    ILogger<TokenQueryService> logger) : ITokenQueryService
{
    public async Task<Result<List<TokenInfo>>> GetAllActiveTokensAsync()
    {
        try
        {
            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .ToListAsync();

            var result = new List<TokenInfo>();

            foreach (var token in tokens)
            {
                var changeResult = await priceChangeService.TakeTokenPriceChangesAsync(token.Symbol, 1);

                var dailyChangePercent = changeResult.TryGetData(out var changeData)
                    ? changeData.ChangePercent
                    : 0m;

                result.Add(TokenInfo.FromEntity(token, dailyChangePercent));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения списка токенов");
            return Fail("Внутренняя ошибка сервера");
        }
    }
}