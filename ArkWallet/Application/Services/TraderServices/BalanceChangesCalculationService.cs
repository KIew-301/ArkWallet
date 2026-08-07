using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;

using static Result<BalanceChangesData>;

internal class BalanceChangesCalculationService(
    ArkWalletDbContext db,
    IBalanceSnapshotService balanceSnapshotService,
    ILogger<BalanceChangesCalculationService> logger) : IBalanceChangesCalculationService
{
    public async Task<Result<BalanceChangesData>> TakeMainBalanceChanges(long traderTelegramId, int periodDays)
    {
        return await CalculateChangesAsync(
            traderTelegramId,
            periodDays,
            snapshot => snapshot.mainBalance,
            snapshot => snapshot.MainBalance);
    }

    public async Task<Result<BalanceChangesData>> TakeTotalBalanceChanges(long traderTelegramId, int periodDays)
    {
        return await CalculateChangesAsync(
            traderTelegramId,
            periodDays,
            snapshot => snapshot.totalBalance,
            snapshot => snapshot.TotalBalance);
    }

    public async Task<Result<BalanceChangesBundle>> TakeBalanceChanges(long traderTelegramId, int periodDays)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (periodDays < 1)
                return Result<BalanceChangesBundle>.Fail("Минимальный период для расчёта: 1 день");

            var currentSnapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderTelegramId);
            if (!currentSnapshotResult.TryGetData(out var currentSnapshot))
                return Result<BalanceChangesBundle>.Fail(currentSnapshotResult.Message);

            var previousSnapshot = await QueryPreviousSnapshotAsync(traderTelegramId, currentSnapshot.dateTimeSnapshot, periodDays);

            return Result<BalanceChangesBundle>.Ok(new BalanceChangesBundle(
                Compute(currentSnapshot, previousSnapshot, s => s.mainBalance, s => s.MainBalance),
                Compute(currentSnapshot, previousSnapshot, s => s.totalBalance, s => s.TotalBalance)));
        }, logger, nameof(BalanceChangesCalculationService));
    }

    private async Task<Result<BalanceChangesData>> CalculateChangesAsync(
        long traderTelegramId,
        int periodDays,
        Func<BalanceSnapshotData, decimal> currentSelector,
        Func<BalanceSnapshot, decimal> previousSelector)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (periodDays < 1)
                return Fail("Минимальный период для расчёта: 1 день");

            var currentSnapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderTelegramId);
            if (!currentSnapshotResult.TryGetData(out var currentSnapshot))
                return Fail(currentSnapshotResult.Message);

            var previousSnapshot = await QueryPreviousSnapshotAsync(traderTelegramId, currentSnapshot.dateTimeSnapshot, periodDays);

            return Ok(Compute(currentSnapshot, previousSnapshot, currentSelector, previousSelector));
        }, logger, nameof(BalanceChangesCalculationService));
    }

    private async Task<BalanceSnapshot?> QueryPreviousSnapshotAsync(long traderTelegramId, DateTime currentSnapshotTime, int periodDays)
    {
        var targetDate = currentSnapshotTime.AddDays(-periodDays);
        return await db.BalanceSnapshots
            .Where(s => s.TraderId == traderTelegramId && s.SnapshotDateTime <= targetDate)
            .OrderByDescending(s => s.SnapshotDateTime)
            .FirstOrDefaultAsync();
    }

    private static BalanceChangesData Compute(
        BalanceSnapshotData currentSnapshot,
        BalanceSnapshot? previousSnapshot,
        Func<BalanceSnapshotData, decimal> currentSelector,
        Func<BalanceSnapshot, decimal> previousSelector)
    {
        var currentBalance = currentSelector(currentSnapshot);
        var previousBalance = previousSnapshot == null
            ? Trader.GetDefaultBalance()
            : previousSelector(previousSnapshot);

        var changeAbsolute = currentBalance - previousBalance;
        var changePercent = previousBalance != 0
            ? changeAbsolute / previousBalance * 100m
            : 0m;

        return new BalanceChangesData(currentBalance, previousBalance, changeAbsolute, changePercent);
    }
}