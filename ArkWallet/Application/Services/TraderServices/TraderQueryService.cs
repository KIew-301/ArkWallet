using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static Result;

internal class TraderQueryService(ArkWalletDbContext dbContext, ILogger<TraderQueryService> logger) : ITraderQueryService
{
    private const long BotIdMin = 100;
    private const long BotIdMax = 1000;

    public async Task<Result<TraderProfileInfo>> GetTraderProfileAsync(long traderTelegramId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderTelegramId);

            if (trader == null)
                return Result<TraderProfileInfo>.Fail("Данные профиля не найдены.");

            return Result<TraderProfileInfo>.Ok(new TraderProfileInfo(trader.Username ?? "Unknown", trader.Balance));
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<List<long>>> GetAllTraderIdsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ids = await dbContext.Traders.Select(t => t.TelegramId).ToListAsync();
            return Result<List<long>>.Ok(ids);
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<int>> GetTraderCountAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var count = await dbContext.Traders
                .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
                .CountAsync();
            return Result<int>.Ok(count);
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<List<(string Username, long TelegramId)>>> GetAllTradersWithoutBotsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var traders = await dbContext.Traders
                .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
                .Select(t => new { t.Username, t.TelegramId })
                .ToListAsync();

            var result = traders
                .Select(t => (t.Username ?? "Unknown", t.TelegramId))
                .ToList();

            return Result<List<(string Username, long TelegramId)>>.Ok(result);
        }, logger, nameof(TraderQueryService));
    }
}
