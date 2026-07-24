using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.PortfolioServices;

internal class PortfolioQueryService(ArkWalletDbContext dbContext, ILogger<PortfolioQueryService> logger) : IPortfolioQueryService
{
    public async Task<Result<PortfolioItemInfo>> GetTokenBalanceAsync(long traderId, string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            if (item == null)
                return Result<PortfolioItemInfo>.Fail("Токен в портфеле не найден");

            return Result<PortfolioItemInfo>.Ok(PortfolioItemInfo.FromEntity(item));
        }, logger, nameof(PortfolioQueryService));
    }

    public async Task<Result<PortfolioItemInfo[]>> GetTraderTokensAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var items = await dbContext.PortfolioItems.Include(p => p.CharacterToken).Where(p => p.TraderTelegramId == traderId).ToListAsync();
            if (items.Count == 0)
                return Result<PortfolioItemInfo[]>.Ok([]);

            return Result<PortfolioItemInfo[]>.Ok([.. items.Where(i => i.Quantity > 0).Select(PortfolioItemInfo.FromEntity)]);
        }, logger, nameof(PortfolioQueryService));
    }
}
