using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.CharacterTokenServices;
using static Result;

internal class TokenDeletionService(ArkWalletDbContext dbContext, ILogger<TokenDeletionService> logger) : ITokenDeletionService
{
    public async Task<Result> DeleteTokenAsync(string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return Fail("Требуется символ токена");

                var token = await dbContext.CharacterTokens
                    .FirstOrDefaultAsync(t => t.Symbol == symbol);

                if (token is null)
                    return Fail($"Токен '{symbol}' не найден");

                var actualSymbol = token.Symbol;

                await dbContext.MarketMakerBots
                    .Where(b => b.Symbol == actualSymbol)
                    .ExecuteDeleteAsync();

                await dbContext.PortfolioItems
                    .Where(p => p.CharacterTokenId == actualSymbol)
                    .ExecuteDeleteAsync();

                await dbContext.TradeOrders
                    .Where(o => o.CharacterTokenId == actualSymbol)
                    .ExecuteDeleteAsync();

                await dbContext.Trades
                    .Where(t => t.CharacterTokenId == actualSymbol)
                    .ExecuteDeleteAsync();

                await dbContext.PriceCandles
                    .Where(c => c.CharacterTokenId == actualSymbol)
                    .ExecuteDeleteAsync();

                dbContext.CharacterTokens.Remove(token);
                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(TokenDeletionService));
    }

    public async Task<Result> DeactivateTokenAsync(string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Result.Fail("Требуется символ токена");

            var token = await dbContext.CharacterTokens
                .FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token is null)
                return Result.Fail($"Токен '{symbol}' не найден");

            var actualSymbol = token.Symbol;

            await dbContext.MarketMakerBots
                .Where(b => b.Symbol == actualSymbol)
                .ExecuteDeleteAsync();

            token.Deactivate();
            await dbContext.SaveChangesAsync();

            return Result.Ok();
        }, logger, nameof(TokenDeletionService));
    }
}
