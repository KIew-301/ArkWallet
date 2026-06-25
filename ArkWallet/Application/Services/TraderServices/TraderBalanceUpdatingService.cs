using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result;

internal class TraderBalanceUpdatingService(ArkWalletDbContext dbContext) : ITraderBalanceUpdatingService
{
    public async Task<Result> AddToBalanceAsync(long traderId, decimal amount)
    {
        if (amount <= 0)
            return Fail("Сумма должна составлять больше 0");

        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

        if (trader == null)
            return Fail("Трейдера не существует");

        trader.AddToBalance(amount);

        dbContext.Traders.Update(trader);
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}
