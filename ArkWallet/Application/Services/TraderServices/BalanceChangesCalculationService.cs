using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;

internal class BalanceChangesCalculationService(ArkWalletDbContext db, BalanceSnapshotService balanceSnapshotService, ILogger<BalanceChangesCalculationService> logger)
{
    public async Task<BalanceChangesCalculationResult> TakeMainBalanceChanges(long traderTelegramId, int periodDays)
    {
        try
        {
            if (periodDays < 1)
                return BalanceChangesCalculationResult.Fail($"Минимальный период для расчёта: 1 день");

            var currentSnapshot = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderTelegramId);
            if (!currentSnapshot.IsSuccess)
                return BalanceChangesCalculationResult.Fail(currentSnapshot.message);

            var targetDate = currentSnapshot.dateTimeSnapshot.Date.AddDays(-periodDays);
            var previousSnapshot = await db.BalanceSnapshots
                .FirstOrDefaultAsync(s => s.TraderId == traderTelegramId && s.SnapshotDateTime.Date >= targetDate);

            if (previousSnapshot == null)
            {
                var currentBalance = currentSnapshot.mainBalance;
                var previousBalance = Trader.GetDefaultBalance();
                var changeAbsolute = currentBalance - previousBalance;
                var сhangePercent = changeAbsolute / previousBalance * 100m;
                return BalanceChangesCalculationResult.Ok(currentBalance, previousBalance, changeAbsolute, сhangePercent);
            }
            else
            {
                var currentBalance = currentSnapshot.mainBalance;
                var previousBalance = previousSnapshot.MainBalance;
                var changeAbsolute = currentBalance - previousBalance;
                var сhangePercent = changeAbsolute / previousBalance * 100m;
                return BalanceChangesCalculationResult.Ok(currentBalance, previousBalance, changeAbsolute, сhangePercent);
            }

        }
        catch (DomainException ex)
        {
            return BalanceChangesCalculationResult.Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка расчёта изменения баланса");
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return BalanceChangesCalculationResult.Fail($"Внутренняя ошибка сервера: {innerMessage}");
        }
    }
}

internal record BalanceChangesCalculationResult(
    bool IsSuccess, string Message, decimal CurrentBalance, decimal PreviousBalance, decimal ChangeAbsolute, decimal ChangePercent)
{
    public static BalanceChangesCalculationResult Ok(decimal сurrentBalance, decimal previousBalance, decimal сhangeAbsolute, decimal сhangePercent)
    {
        return new(true, "Данные рассчитаны успешно", сurrentBalance, previousBalance, сhangeAbsolute, сhangePercent);
    }

    public static BalanceChangesCalculationResult Fail(string message)
    {
        return new(false, message, 0, 0, 0, 0);
    }
}
