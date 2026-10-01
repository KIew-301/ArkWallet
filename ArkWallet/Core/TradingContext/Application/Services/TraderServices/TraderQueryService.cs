using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using static Result;

internal class TraderQueryService(ArkWalletDbContext dbContext, ILogger<TraderQueryService> logger) : ITraderQueryService
{
    public async Task<Result<TraderProfileInfo>> GetTraderProfileAsync(long traderTelegramId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == traderTelegramId);

            if (trader == null)
                return Result<TraderProfileInfo>.Fail("Данные профиля не найдены.");

            return Result<TraderProfileInfo>.Ok(new TraderProfileInfo(trader.Username ?? "Unknown", trader.Balance));
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<List<long>>> GetAllTraderIdsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ids = await dbContext.Traders.Select(t => t.Id).ToListAsync();
            return Result<List<long>>.Ok(ids);
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<int>> GetTraderCountAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var count = await dbContext.Traders
                .Where(t => !t.IsBot)
                .CountAsync();
            return Result<int>.Ok(count);
        }, logger, nameof(TraderQueryService));
    }

    public async Task<Result<List<(string Username, long? TelegramId)>>> GetAllTradersWithoutBotsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var traders = await dbContext.Traders
                .Where(t => !t.IsBot)
                .Select(t => new { t.Username, t.TelegramId })
                .ToListAsync();

            var result = traders
                .Select(t => (t.Username ?? "Unknown", t.TelegramId))
                .ToList();

            return Result<List<(string Username, long? TelegramId)>>.Ok(result);
        }, logger, nameof(TraderQueryService));
    }

    public async Task<long> GetTraderIdByTelegramIdAsync(long telegramId)
    {
        var trader = await dbContext.Traders
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TelegramId == telegramId);

        return trader?.Id ?? 0;
    }
}
