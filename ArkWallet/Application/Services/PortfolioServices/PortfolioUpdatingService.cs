using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.PortfolioServices;
using static Result;

internal class PortfolioUpdatingService(ArkWalletDbContext dbContext) : IPortfolioUpdatingService
{
    public async Task<Result> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity)
    {
        if (quantity <= 0)
            return Fail("Для обновление портфеля необходим минимум один токен");

        var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
        var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);

        if (token == null)
            return Fail("Токена не существует");

        if (item == null)
        {
            item = PortfolioItem.Create(traderId, symbol, quantity, token.CurrentPrice);
            await dbContext.PortfolioItems.AddAsync(item);
        }
        else
        {
            item.BuyTokens(quantity, token.CurrentPrice);
        }

        await dbContext.SaveChangesAsync();

        return Ok();
    }
}
