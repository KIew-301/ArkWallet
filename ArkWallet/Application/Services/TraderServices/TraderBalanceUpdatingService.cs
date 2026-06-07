using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TraderServices
{
    internal class TraderBalanceUpdatingService(ArkWalletDbContext dbContext) : ITraderBalanceUpdatingService
    {
        public async Task<TraderBalanceUpdatingResult> AddToBalanceAsync(long traderId, decimal amount)
        {
            if (amount <= 0)
                return new TraderBalanceUpdatingResult(false, "Число должно быть больше 0");

            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

            if (trader == null)
                return new TraderBalanceUpdatingResult(false, "Трейдера не существует");

            trader.AddToBalance(amount);

            dbContext.Traders.Update(trader);
            await dbContext.SaveChangesAsync();

            return new TraderBalanceUpdatingResult(true);
        }
    }
}
