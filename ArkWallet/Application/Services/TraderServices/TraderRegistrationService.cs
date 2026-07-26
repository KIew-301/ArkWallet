using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static ArkWallet.Application.Common.Result;

internal class TraderRegistrationService(ArkWalletDbContext dbContext, ILogger<TraderRegistrationService> logger) : ITraderRegistrationService
{
    public async Task<Result> RegisterTraderAsync(long telegramId, string name, bool enableNotyfi = true)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Fail("Имя не может быть пустым");

            if (telegramId <= 0)
                return Fail($"Некорректный ID пользователя {telegramId}");

            var isRegistered = await CheckTraderAlreadyRegistered(telegramId);

            if (isRegistered)
                return Fail("Пользователь уже существует");

            var trader = Trader.Create(telegramId, name);

            if (!enableNotyfi)
                trader.NotificationOn = false;

            await dbContext.Traders.AddAsync(trader);
            await dbContext.SaveChangesAsync();

            return Ok();
        }, logger, nameof(TraderRegistrationService));
    }

    public async Task<bool> CheckTraderAlreadyRegistered(long telegramId)
    {
        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);

        if (trader == null)
            return false;
        else
            return true;
    }

    public async Task<Result<List<long>>> GetAllTraderIdsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ids = await dbContext.Traders.Select(t => t.TelegramId).ToListAsync();
            return Result<List<long>>.Ok(ids);
        }, logger, nameof(TraderRegistrationService));
    }

    public async Task<Result<int>> GetTraderCountAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var count = await dbContext.Traders.CountAsync();
            return Result<int>.Ok(count);
        }, logger, nameof(TraderRegistrationService));
    }
}
