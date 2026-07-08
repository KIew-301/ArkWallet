using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Application.Workers;

internal class BalanceSavingSnapshotWorker(
    IServiceProvider serviceProvider,
    ILogger<BalanceSavingSnapshotWorker> logger) : BackgroundService
{
    private const int UpdatePeriodInSeconds = 60 * 60 * 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();

                DateTime? lastUpdate = db.AppStates.Find("balanceSnapshotsLastUpdate")?.GetValue<DateTime>();
                DateTime now = DateTime.UtcNow;

                if (lastUpdate == null || now.AddSeconds(-UpdatePeriodInSeconds) > lastUpdate)
                    await CreateAndSaveSnaphotsForAllTraders(db, scope);

                await Task.Delay(TimeSpan.FromSeconds(UpdatePeriodInSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in BalanceSavingSnapshotWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task CreateAndSaveSnaphotsForAllTraders(ArkWalletDbContext db, IServiceScope scope)
    {
        var ids = db.Traders.Select(t => t.TelegramId).ToArray();

        var balanceSnapshotService = scope.ServiceProvider.GetRequiredService<IBalanceSnapshotService>();
        var balanceSavingService = scope.ServiceProvider.GetRequiredService<IBalanceSavingService>();

        using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var id in ids)
        {
            var snaphotResult = await balanceSnapshotService.TakeTotalTraderBalanceSnapshot(id);

            if (snaphotResult.TryGetData(out var snapshot))
            {
                var savintResult = await balanceSavingService.SaveBalanceToDatabase(
                    snapshot.traderTelegramId,
                    snapshot.totalBalance,
                    snapshot.mainBalance,
                    snapshot.longOrderReserve,
                    snapshot.shortOrderReserve,
                    snapshot.balanceInTokens,
                    snapshot.dateTimeSnapshot
                );

                if (!savintResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Ошибка сохранения снимка баланса");
                }
            }
            else
            {
                await transaction.RollbackAsync();
                throw new Exception("Ошибка в создании снимка баланса");
            }
        }

        DateTime now = DateTime.UtcNow;

        var state = db.AppStates.Find("balanceSnapshotsLastUpdate");

        if (state == null)
        {
            state = AppState.Create("balanceSnapshotsLastUpdate", now);
            db.AppStates.Add(state);
        }
        else
        {
            state.UpdateValue(now);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
