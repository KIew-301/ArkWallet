using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.PortfolioServices;

internal class PortfolioQueryService(ArkWalletDbContext dbContext, ILogger<PortfolioQueryService> logger) : IPortfolioQueryService
{
    public async Task<Result<TokenBalanceDto>> GetTokenBalanceAsync(long traderId, string symbol)
    {
        try
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            if (item == null)
                return Result<TokenBalanceDto>.Fail("Токен в портфеле не найден");

            return Result<TokenBalanceDto>.Ok(TokenBalanceDto.FromEntity(item));
        }
        catch (DomainException ex)
        {
            return Result<TokenBalanceDto>.Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка расчёта изменений баланса");
            return Result<TokenBalanceDto>.Fail($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<Result<TokenBalanceDto[]>> GetTraderTokensAsync(long traderId)
    {
        try
        {
            var items = await dbContext.PortfolioItems.Include(p => p.CharacterToken).Where(p => p.TraderTelegramId == traderId).ToListAsync();
            if (items.Count == 0)
                return Result<TokenBalanceDto[]>.Ok([]);

            return Result<TokenBalanceDto[]>.Ok([.. items.Select(TokenBalanceDto.FromEntity)]);
        }
        catch (DomainException ex)
        {
            return Result<TokenBalanceDto[]>.Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка расчёта изменений баланса");
            return Result<TokenBalanceDto[]>.Fail($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
