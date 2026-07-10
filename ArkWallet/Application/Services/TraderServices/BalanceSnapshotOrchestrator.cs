using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;

using static Result;

internal class BalanceSnapshotOrchestrator(
    ArkWalletDbContext dbContext,
    IBalanceSnapshotService balanceSnapshotService,
    IBalanceSavingService balanceSavingService,
    ILogger<BalanceSnapshotOrchestrator> logger) : IBalanceSnapshotOrchestrator
{
    public async Task<Result> CreateSnapshotsForAllTradersAsync()
    {
        try
        {
            var traderIds = await dbContext.Traders
                .Select(t => t.TelegramId)
                .ToArrayAsync();

            if (traderIds.Length == 0)
            {
                logger.LogWarning("Нет трейдеров для создания снимков");
                return Ok();
            }

            using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                foreach (var traderId in traderIds)
                {
                    var snapshotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(traderId);

                    if (!snapshotResult.TryGetData(out var snapshot))
                    {
                        await transaction.RollbackAsync();
                        logger.LogError("Ошибка создания снимка для трейдера {TraderId}: {Error}", traderId, snapshotResult.Message);
                        return Fail($"Ошибка создания снимка для трейдера {traderId}: {snapshotResult.Message}");
                    }

                    var savingResult = await balanceSavingService.SaveBalanceToDatabase(
                        snapshot.traderTelegramId,
                        snapshot.totalBalance,
                        snapshot.mainBalance,
                        snapshot.longOrderReserve,
                        snapshot.shortOrderReserve,
                        snapshot.balanceInTokens,
                        snapshot.dateTimeSnapshot
                    );

                    if (!savingResult.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        logger.LogError("Ошибка сохранения снимка для трейдера {TraderId}: {Error}", traderId, savingResult.Message);
                        return Fail($"Ошибка сохранения снимка для трейдера {traderId}: {savingResult.Message}");
                    }
                }

                await transaction.CommitAsync();
                logger.LogInformation("Снимки баланса созданы для {Count} трейдеров", traderIds.Length);
                return Ok();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании снимков баланса");
            return Fail($"Внутренняя ошибка сервера: {ex.Message}");
        }
    }
}
