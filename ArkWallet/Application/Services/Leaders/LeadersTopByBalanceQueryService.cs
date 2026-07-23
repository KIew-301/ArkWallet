using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Leaders;

using static ArkWallet.Application.Common.Result<List<LeaderEntry>>;

internal class LeadersTopByBalanceQueryService(
    ArkWalletDbContext dbContext,
    IBalanceSnapshotService balanceSnapshotService,
    ILogger<LeadersTopByBalanceQueryService> logger) : ILeadersTopByBalanceQueryService
{
    private const int MaxLeaderboardSize = 100;
    private const long BotIdMin = 100;
    private const long BotIdMax = 1000;

    public async Task<Result<List<LeaderEntry>>> GetTopAsync(int count)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            count = Math.Clamp(count, 1, MaxLeaderboardSize);

            var traders = await dbContext.Traders
                .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
                .ToListAsync();

            traders = traders
                .OrderByDescending(t => t.Balance)
                .Take(count)
                .ToList();

            var entries = new List<LeaderEntry>();
            var position = 1;

            foreach (var trader in traders)
            {
                var snapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(trader.TelegramId);

                if (!snapshotResult.IsSuccess || !snapshotResult.TryGetData(out var snapshot))
                    return Fail($"Не удалось рассчитать баланс трейдера {trader.Username ?? trader.TelegramId.ToString()}");

                entries.Add(new LeaderEntry(
                    position++,
                    trader.TelegramId,
                    trader.Username ?? "Аноним",
                    snapshot.totalBalance));
            }

            return Ok(entries);
        }, logger, nameof(LeadersTopByBalanceQueryService));
    }

    public async Task<Result<LeaderPosition>> GetTraderPositionAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var snapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderId);

            if (!snapshotResult.IsSuccess || !snapshotResult.TryGetData(out var snapshotData))
                return Result<LeaderPosition>.Fail("Не удалось рассчитать баланс трейдера");

            decimal totalBalance = snapshotData.totalBalance;

            var allTraderIds = (await dbContext.Traders
                .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
                .ToListAsync())
                .OrderByDescending(t => t.Balance)
                .Take(MaxLeaderboardSize)
                .Select(t => t.TelegramId)
                .ToList();

            if (!allTraderIds.Contains(traderId))
            {
                allTraderIds.Add(traderId);
            }

            var entries = new List<(long TelegramId, decimal TotalBalance)>();

            foreach (var id in allTraderIds)
            {
                decimal traderTotal;
                if (id == traderId)
                {
                    traderTotal = totalBalance;
                }
                else
                {
                    var snap = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(id);
                    if (!snap.IsSuccess || !snap.TryGetData(out var s))
                        return Result<LeaderPosition>.Fail($"Не удалось рассчитать баланс трейдера {id}");
                    traderTotal = s.totalBalance;
                }

                entries.Add((id, traderTotal));
            }

            var sorted = entries
                .OrderByDescending(e => e.TotalBalance)
                .ToList();

            var position = sorted.FindIndex(e => e.TelegramId == traderId) + 1;

            return Result<LeaderPosition>.Ok(new LeaderPosition(position, sorted.Count, totalBalance));
        }, logger, nameof(LeadersTopByBalanceQueryService));
    }

    public async Task<Result<List<LeaderEntry>>> GetLocalTopAsync(long traderId, int aboveCount, int belowCount)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            aboveCount = Math.Clamp(aboveCount, 0, 10);
            belowCount = Math.Clamp(belowCount, 0, 10);

            var positionResult = await GetTraderPositionAsync(traderId);
            decimal myBalance = 0m;
            int myPosition = 1;

            if (positionResult.IsSuccess && positionResult.TryGetData(out var posData))
            {
                myBalance = posData.TotalBalance;
                myPosition = posData.Position;
            }

            var allTraderIds = (await dbContext.Traders
                .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
                .ToListAsync())
                .OrderByDescending(t => t.Balance)
                .Take(MaxLeaderboardSize)
                .Select(t => new { t.TelegramId, t.Username })
                .ToList();

            var entries = new List<(long TelegramId, string Username, decimal TotalBalance)>();

            foreach (var trader in allTraderIds)
            {
                decimal traderTotal;
                if (trader.TelegramId == traderId)
                {
                    traderTotal = myBalance;
                }
                else
                {
                    var snap = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(trader.TelegramId);
                    if (!snap.IsSuccess || !snap.TryGetData(out var s))
                        return Fail($"Не удалось рассчитать баланс трейдера {trader.Username ?? trader.TelegramId.ToString()}");
                    traderTotal = s.totalBalance;
                }

                entries.Add((trader.TelegramId, trader.Username ?? "Аноним", traderTotal));
            }

            if (entries.All(e => e.TelegramId != traderId))
            {
                entries.Add((traderId, "Аноним", myBalance));
            }

            var sorted = entries
                .OrderByDescending(e => e.TotalBalance)
                .ToList();

            var traderIndex = sorted.FindIndex(e => e.TelegramId == traderId);
            if (traderIndex < 0)
                return Fail("Трейдер не найден в рейтинге");

            var startIdx = Math.Max(0, traderIndex - aboveCount);
            var endIdx = Math.Min(sorted.Count - 1, traderIndex + belowCount);

            var result = new List<LeaderEntry>();
            for (int i = startIdx; i <= endIdx; i++)
            {
                result.Add(new LeaderEntry(
                    i + 1,
                    sorted[i].TelegramId,
                    sorted[i].Username,
                    sorted[i].TotalBalance));
            }

            return Ok(result);
        }, logger, nameof(LeadersTopByBalanceQueryService));
    }
}
