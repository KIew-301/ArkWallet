using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.PortfolioServices
{
    internal class PortfolioUpdatingService(ArkWalletDbContext dbContext) : IPortfolioUpdatingService
    {
        public async Task<PortfolioUpdatingResult> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity)
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == t.Symbol);

            if (token == null)
                return new PortfolioUpdatingResult(false, "Токена не существует");

            if (item == null)
                item = PortfolioItem.Create(traderId, symbol, quantity, token.CurrentPrice);
            else
                item.AddTokens(quantity, token.CurrentPrice);

            dbContext.PortfolioItems.Update(item);
            await dbContext.SaveChangesAsync();

            return new PortfolioUpdatingResult(true);
        }
    }
}
