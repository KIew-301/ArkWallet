using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static Result<BalanceChangesData>;

internal class BalanceChangesCalculationService(
    ArkWalletDbContext db, IBalanceSnapshotService balanceSnapshotService,
    ILogger<BalanceChangesCalculationService> logger)
{
    public async Task<Result<BalanceChangesData>> TakeMainBalanceChanges(long traderTelegramId, int periodDays)
    {
        try
        {
            if (periodDays < 1)
                return Fail($"Минимальный период для расчёта: 1 день");

            var currentSnapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderTelegramId);
            if (!currentSnapshotResult.TryGetData(out var currentSnapshot))
                return Fail(currentSnapshotResult.Message);

            var targetDate = currentSnapshot.dateTimeSnapshot.Date.AddDays(-periodDays);
            var previousSnapshot = await db.BalanceSnapshots
                .FirstOrDefaultAsync(s => s.TraderId == traderTelegramId && s.SnapshotDateTime.Date >= targetDate);

            if (previousSnapshot == null)
            {
                var currentBalance = currentSnapshot.mainBalance;
                var previousBalance = Trader.GetDefaultBalance();
                var changeAbsolute = currentBalance - previousBalance;
                var сhangePercent = changeAbsolute / previousBalance * 100m;
                return Ok(new BalanceChangesData(currentBalance, previousBalance, changeAbsolute, сhangePercent));
            }
            else
            {
                var currentBalance = currentSnapshot.mainBalance;
                var previousBalance = previousSnapshot.MainBalance;
                var changeAbsolute = currentBalance - previousBalance;
                var сhangePercent = changeAbsolute / previousBalance * 100m;
                return Ok(new BalanceChangesData(currentBalance, previousBalance, changeAbsolute, сhangePercent));
            }

        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка расчёта изменения баланса");
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return Fail($"Внутренняя ошибка сервера: {innerMessage}");
        }
    }
}

internal record BalanceChangesData(decimal CurrentBalance, decimal PreviousBalance, decimal ChangeAbsolute, decimal ChangePercent);
