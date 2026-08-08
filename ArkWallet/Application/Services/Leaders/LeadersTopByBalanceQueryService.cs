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

    private async Task<List<(long TelegramId, string Username, decimal TotalBalance)>> GetAllTradersWithBalances()
    {
        var traders = await dbContext.Traders
            .Where(t => t.TelegramId < BotIdMin || t.TelegramId > BotIdMax)
            .ToListAsync();

        var snapshots = await balanceSnapshotService.TakeTotalTraderBalanceSnapshotsAsync(
            traders.Select(t => t.TelegramId));
        if (!snapshots.IsSuccess || !snapshots.TryGetData(out var snapshotByTrader))
            return null!;

        return traders
            .Select(t => (t.TelegramId, t.Username ?? "Аноним", snapshotByTrader.GetValueOrDefault(t.TelegramId).totalBalance))
            .OrderByDescending(e => e.Item3)
            .ToList();
    }

    public async Task<Result<List<LeaderEntry>>> GetTopAsync(int count)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            count = Math.Clamp(count, 1, MaxLeaderboardSize);

            var sorted = await GetAllTradersWithBalances();
            if (sorted == null)
                return Fail("Не удалось рассчитать баланс одного из трейдеров");

            var entries = sorted
                .Take(count)
                .Select((e, i) => new LeaderEntry(i + 1, e.TelegramId, e.Username, e.TotalBalance))
                .ToList();

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

            var sorted = await GetAllTradersWithBalances();
            if (sorted == null)
                return Result<LeaderPosition>.Fail("Не удалось рассчитать баланс одного из трейдеров");

            var traderIds = sorted.Select(e => e.TelegramId).ToList();
            if (!traderIds.Contains(traderId))
                traderIds.Add(traderId);

            var entries = new List<(long TelegramId, decimal TotalBalance)>();
            foreach (var id in traderIds)
            {
                if (id == traderId)
                {
                    entries.Add((id, totalBalance));
                }
                else
                {
                    var found = sorted.FirstOrDefault(e => e.TelegramId == id);
                    entries.Add((id, found.TotalBalance));
                }
            }

            var ranked = entries.OrderByDescending(e => e.TotalBalance).ToList();
            var position = ranked.FindIndex(e => e.TelegramId == traderId) + 1;

            return Result<LeaderPosition>.Ok(new LeaderPosition(position, ranked.Count, totalBalance));
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

            if (positionResult.IsSuccess && positionResult.TryGetData(out var posData))
            {
                myBalance = posData.TotalBalance;
            }

            var sorted = await GetAllTradersWithBalances();
            if (sorted == null)
                return Fail("Не удалось рассчитать баланс одного из трейдеров");

            var entries = sorted
                .Select(e => (e.TelegramId, e.Username, e.TotalBalance))
                .ToList();

            if (entries.All(e => e.TelegramId != traderId))
            {
                entries.Add((traderId, "Аноним", myBalance));
            }

            var ranked = entries.OrderByDescending(e => e.TotalBalance).ToList();
            var traderIndex = ranked.FindIndex(e => e.TelegramId == traderId);
            if (traderIndex < 0)
                return Fail("Трейдер не найден в рейтинге");

            var startIdx = Math.Max(0, traderIndex - aboveCount);
            var endIdx = Math.Min(ranked.Count - 1, traderIndex + belowCount);

            var result = new List<LeaderEntry>();
            for (int i = startIdx; i <= endIdx; i++)
            {
                result.Add(new LeaderEntry(
                    i + 1,
                    ranked[i].TelegramId,
                    ranked[i].Username,
                    ranked[i].TotalBalance));
            }

            return Ok(result);
        }, logger, nameof(LeadersTopByBalanceQueryService));
    }
}
