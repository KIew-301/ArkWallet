using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result;

internal class TraderRegistrationService(ArkWalletDbContext dbContext) : ITraderRegistrationService
{
    public async Task<Result> RegisterTraderAsync(long telegramId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Fail("Имя не может быть пустым");

        if (telegramId <= 0)
            return Fail($"Некорректный ID пользователя {telegramId}");

        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);

        if (trader != null)
            return Fail("Пользователь уже существует");

        trader = Trader.Create(telegramId, name);

        await dbContext.Traders.AddAsync(trader);
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}
