using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.PortfolioContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.PortfolioServices;
using static Result;

/// <summary>
/// Thin scribe for portfolio mutations. Loads a position, delegates to a single aggregate method
/// that owns the business rules, then persists. Never decides business logic itself.
/// </summary>
internal class PortfolioUpdatingService(ArkWalletDbContext dbContext, ILogger<PortfolioUpdatingService> logger) : IPortfolioUpdatingService
{
    public async Task<Result> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);
                await dbContext.LockTokenAsync(symbol);

                var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
                if (token == null)
                    return Fail("Токена не существует");

                var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);

                if (item == null)
                {
                    var position = Position.Create(traderId, symbol, quantity, token.CurrentPrice);
                    await dbContext.PortfolioItems.AddAsync(PortfolioContextMapper.ToRecord(position));
                }
                else
                {
                    var position = PortfolioContextMapper.ToPosition(item);
                    position.CreateOrUpdate(quantity, token.CurrentPrice);
                    PortfolioContextMapper.ApplyToRecord(item, position);
                }

                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(PortfolioUpdatingService));
    }

    public async Task<Result> ChangePositionAsync(PortfolioChangeCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([command.TraderId]);
                await dbContext.LockTokenAsync(command.Symbol);

                var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == command.TraderId && p.CharacterTokenId == command.Symbol);

                if (item == null)
                {
                    if (command.Type is PortfolioChangeType.Buy or PortfolioChangeType.Add)
                    {
                        var price = await GetTokenPriceAsync(command.Symbol);
                        var position = Position.Create(command.TraderId, command.Symbol, command.Quantity, price);
                        await dbContext.PortfolioItems.AddAsync(PortfolioContextMapper.ToRecord(position));
                        await dbContext.SaveChangesAsync();
                        return Ok();
                    }

                    return Fail("Позиция в портфеле не найдена");
                }

                var aggregate = PortfolioContextMapper.ToPosition(item);
                aggregate.ChangePosition(command);
                PortfolioContextMapper.ApplyToRecord(item, aggregate);

                if (aggregate.IsEmpty)
                    dbContext.PortfolioItems.Remove(item);

                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(PortfolioUpdatingService));
    }

    private async Task<decimal> GetTokenPriceAsync(string symbol)
    {
        var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
        return token?.CurrentPrice ?? 0;
    }
}
