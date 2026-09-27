using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.TradingContext.Application.Services.TraderServices;

using static Result<BalanceChangesData>;

internal class BalanceChangesCalculationService(
    ArkWalletDbContext db,
    IBalanceSnapshotService balanceSnapshotService,
    ILogger<BalanceChangesCalculationService> logger) : IBalanceChangesCalculationService
{
    public async Task<Result<BalanceChangesData>> TakeMainBalanceChanges(long traderId, int periodDays)
    {
        return await CalculateChangesAsync(
            traderId,
            periodDays,
            snapshot => snapshot.mainBalance,
            snapshot => snapshot.MainBalance);
    }

    public async Task<Result<BalanceChangesData>> TakeTotalBalanceChanges(long traderId, int periodDays)
    {
        return await CalculateChangesAsync(
            traderId,
            periodDays,
            snapshot => snapshot.totalBalance,
            snapshot => snapshot.TotalBalance);
    }

    public async Task<Result<BalanceChangesBundle>> TakeBalanceChanges(long traderId, int periodDays)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (periodDays < 1)
                return Result<BalanceChangesBundle>.Fail("Минимальный период для расчёта: 1 день");

            var currentSnapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderId);
            if (!currentSnapshotResult.TryGetData(out var currentSnapshot))
                return Result<BalanceChangesBundle>.Fail(currentSnapshotResult.Message);

            var previousSnapshot = await QueryPreviousSnapshotAsync(traderId, currentSnapshot.dateTimeSnapshot, periodDays);

            return Result<BalanceChangesBundle>.Ok(new BalanceChangesBundle(
                Compute(currentSnapshot, previousSnapshot, s => s.mainBalance, s => s.MainBalance),
                Compute(currentSnapshot, previousSnapshot, s => s.totalBalance, s => s.TotalBalance)));
        }, logger, nameof(BalanceChangesCalculationService));
    }

    private async Task<Result<BalanceChangesData>> CalculateChangesAsync(
        long traderId,
        int periodDays,
        Func<BalanceSnapshotData, decimal> currentSelector,
        Func<BalanceSnapshot, decimal> previousSelector)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (periodDays < 1)
                return Fail("Минимальный период для расчёта: 1 день");

            var currentSnapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderId);
            if (!currentSnapshotResult.TryGetData(out var currentSnapshot))
                return Fail(currentSnapshotResult.Message);

            var previousSnapshot = await QueryPreviousSnapshotAsync(traderId, currentSnapshot.dateTimeSnapshot, periodDays);

            return Ok(Compute(currentSnapshot, previousSnapshot, currentSelector, previousSelector));
        }, logger, nameof(BalanceChangesCalculationService));
    }

    private async Task<BalanceSnapshot?> QueryPreviousSnapshotAsync(long traderId, DateTime currentSnapshotTime, int periodDays)
    {
        var targetDate = currentSnapshotTime.AddDays(-periodDays);
        return await db.BalanceSnapshots
            .Where(s => s.TraderId == traderId && s.SnapshotDateTime <= targetDate)
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
            ? Trader.DefaultBalance
            : previousSelector(previousSnapshot);

        var changeAbsolute = currentBalance - previousBalance;
        var changePercent = previousBalance != 0
            ? changeAbsolute / previousBalance * 100m
            : 0m;

        return new BalanceChangesData(currentBalance, previousBalance, changeAbsolute, changePercent);
    }
}
