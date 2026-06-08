using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Application.Services.Other;
using ArkWallet.Infrastructure.Data;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.PortfolioServices
{
    internal class PortfolioQueryService(ArkWalletDbContext dbContext, ReserveCalculationService reserveCalculationService) : IPortfolioQueryService
    {
        public async Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol)
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            return TokenBalanceDto.FromEntity(item);
        }

        public async Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId)
        {
            var items = await dbContext.PortfolioItems.Where(p => p.TraderTelegramId == traderId).ToListAsync();

            if (items.Count == 0)
                return [];

            return [.. items.Select(TokenBalanceDto.FromEntity)];
        }

        public async Task<TokenBalanceDto?> GetAvailableTokenBalanceAsync(long traderId, string symbol)
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            var reserve = await reserveCalculationService.GetReservedQuantityAsync(traderId, symbol);

            return TokenBalanceDto.FromEntity(item, reserve);
        }

        public async Task<List<TokenBalanceDto>> GetAvailableTraderTokensAsync(long traderId)
        {
            var items = await dbContext.PortfolioItems.Where(p => p.TraderTelegramId == traderId).ToListAsync();
            var reserve = await reserveCalculationService.GetReservedQuantitiesAllAsync(traderId);

            if (items.Count == 0)
                return [];

            return [.. items.Select(i => TokenBalanceDto.FromEntity(i, reserve.GetValueOrDefault(i.CharacterTokenId, 0)))];
        }
    }
}
