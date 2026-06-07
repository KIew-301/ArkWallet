using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TraderServices
{
    internal class TraderRegistrationService(ArkWalletDbContext dbContext) : ITraderRegistrationService
    {
        public async Task<RegistrationResult> RegisterTraderAsync(long telegramId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new RegistrationResult(false, "Имя не может быть пустым");

            if (telegramId <= 0)
                return new RegistrationResult(false, "Некорректный ID пользователя");

            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);

            if (trader != null)
                return new RegistrationResult(false, "Пользователь уже существует");

            trader = Trader.Create(telegramId, name);

            await dbContext.Traders.AddAsync(trader);
            await dbContext.SaveChangesAsync();

            return new RegistrationResult(true);
        }
    }
}
