using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result;

internal class TraderBalanceUpdatingService(ArkWalletDbContext dbContext, ILogger<TraderBalanceUpdatingService> logger) : ITraderBalanceUpdatingService
{
    public async Task<Result> AddToBalanceAsync(long traderId, decimal amount)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (amount <= 0)
                return Fail("Сумма должна составлять больше 0");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);

                var trader = await dbContext.Traders
                    .AsTracking()
                    .FirstOrDefaultAsync(t => t.TelegramId == traderId);

                if (trader == null)
                    return Fail("Трейдера не существует");

                trader.AddToBalance(amount);

                dbContext.Traders.Update(trader);
                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(TraderBalanceUpdatingService));
    }
}
