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

            var targetDate = currentSnapshot.dateTimeSnapshot.AddDays(-periodDays);
            var previousSnapshot = await db.BalanceSnapshots
                .Where(s => s.TraderId == traderTelegramId && s.SnapshotDateTime <= targetDate)
                .OrderByDescending(s => s.SnapshotDateTime)
                .FirstOrDefaultAsync();

            var currentBalance = currentSelector(currentSnapshot);
            var previousBalance = previousSnapshot == null
                ? Trader.GetDefaultBalance()
                : previousSelector(previousSnapshot);

            var changeAbsolute = currentBalance - previousBalance;
            var changePercent = previousBalance != 0
                ? changeAbsolute / previousBalance * 100m
                : 0m;

            return Ok(new BalanceChangesData(currentBalance, previousBalance, changeAbsolute, changePercent));
        }, logger, nameof(BalanceChangesCalculationService));
    }
}