using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Infrastructure.Data;
using Records = global::ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices;
using static Result;

/// <summary>
/// Thin scribe for portfolio mutations. Loads a position, delegates to a single aggregate method
/// that owns the business rules, then persists. Never decides business logic itself.
/// </summary>
internal class UpdatingService(ArkWalletDbContext dbContext, ILogger<UpdatingService> logger) : IUpdatingService
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
                    var position = CreateNewPosition(traderId, symbol, quantity, token.CurrentPrice);
                    return await SaveNewAsync(position);
                }
                else
                {
                    var position = ToExistingPosition(item);
                    position.CreateOrUpdate(quantity, token.CurrentPrice);
                    return await SaveUpdatedAsync(item, position);
                }
            });
        }, logger, nameof(UpdatingService));
    }

    public async Task<Result> ChangePositionAsync(ChangeCommand command)
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
                        var position = CreateNewPosition(command.TraderId, command.Symbol, command.Quantity, price);
                        return await SaveNewAsync(position);
                    }

                    return Fail("Позиция в портфеле не найдена");
                }

                var aggregate = ToExistingPosition(item);
                aggregate.ChangePosition(command);

                if (aggregate.IsEmpty)
                {
                    dbContext.PortfolioItems.Remove(item);
                    return await SaveChangesAsync();
                }

                return await SaveUpdatedAsync(item, aggregate);
            });
        }, logger, nameof(UpdatingService));
    }

    private async Task<decimal> GetTokenPriceAsync(string symbol)
    {
        var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
        return token?.CurrentPrice ?? 0;
    }

    private async Task<Result> SaveNewAsync(Position position)
    {
        await dbContext.PortfolioItems.AddAsync(Mapper.ToRecord(position));
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    private async Task<Result> SaveUpdatedAsync(Records.PortfolioItem item, Position position)
    {
        Mapper.ApplyToRecord(item, position);
        return await SaveChangesAsync();
    }

    private async Task<Result> SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    private static Position CreateNewPosition(long traderId, string symbol, int quantity, decimal price)
        => Position.Create(traderId, symbol, quantity, price);

    private static Position ToExistingPosition(Records.PortfolioItem item)
        => Mapper.ToPosition(item);
}
