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

    public async Task<Result<List<LeaderEntry>>> GetTopAsync(int count)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            count = Math.Clamp(count, 1, MaxLeaderboardSize);

            var traders = await dbContext.Traders
                .OrderByDescending(t => t.Balance)
                .Take(count)
                .ToListAsync();

            var entries = new List<LeaderEntry>();
            var position = 1;

            foreach (var trader in traders)
            {
                var snapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(trader.TelegramId);

                decimal totalBalance = snapshotResult.IsSuccess && snapshotResult.TryGetData(out var snapshot)
                    ? snapshot.totalBalance
                    : trader.Balance;

                entries.Add(new LeaderEntry(
                    position++,
                    trader.TelegramId,
                    trader.Username ?? "Аноним",
                    totalBalance));
            }

            return Ok(entries);
        }, logger, nameof(LeadersTopByBalanceQueryService));
    }

    public async Task<Result<LeaderPosition>> GetTraderPositionAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var snapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderId);

            decimal totalBalance = snapshotResult.IsSuccess && snapshotResult.TryGetData(out var snapshot)
                ? snapshot.totalBalance
                : 0m;

            var allTraderIds = await dbContext.Traders
                .OrderByDescending(t => t.Balance)
                .Take(MaxLeaderboardSize)
                .Select(t => t.TelegramId)
                .ToListAsync();

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
                    traderTotal = snap.IsSuccess && snap.TryGetData(out var s) ? s.totalBalance : 0m;
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

            var allTraderIds = await dbContext.Traders
                .OrderByDescending(t => t.Balance)
                .Take(MaxLeaderboardSize)
                .Select(t => new { t.TelegramId, t.Username })
                .ToListAsync();

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
                    traderTotal = snap.IsSuccess && snap.TryGetData(out var s) ? s.totalBalance : 0m;
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
