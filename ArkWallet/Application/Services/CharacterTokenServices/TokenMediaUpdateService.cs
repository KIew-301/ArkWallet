using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;

internal class TokenMediaUpdateService(
    ArkWalletDbContext dbContext,
    ILogger<TokenMediaUpdateService> logger) : ITokenMediaUpdateService
{
    public async Task<Result> UpdateTokenMediaAsync(string symbol, string iconUrl, string imageUrl)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Result.Fail("Требуется символ токена");

            if (string.IsNullOrWhiteSpace(iconUrl))
                return Result.Fail("Требуется URL иконки");

            if (string.IsNullOrWhiteSpace(imageUrl))
                return Result.Fail("Требуется URL изображения");

            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token is null)
                return Result.Fail($"Токен '{symbol}' не найден");

            token.UpdateMedia(iconUrl, imageUrl);

            await dbContext.SaveChangesAsync();

            return Result.Ok();
        }, logger, nameof(TokenMediaUpdateService));
    }
}
