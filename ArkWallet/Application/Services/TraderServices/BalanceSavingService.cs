using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static Result;

internal class BalanceSavingService(
    ArkWalletDbContext db, ILogger<BalanceSavingService> logger) : IBalanceSavingService
{
    public async Task<Result> SaveBalanceToDatabase(
        long traderTelegramId,
        decimal totalBalance,
        decimal mainBalance,
        decimal longOrderReserve,
        decimal shortOrderReserve,
        decimal balanceInTokens,
        DateTime snapshotDateTime)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (snapshotDateTime == default)
                return Fail($"Некорректная дата и время снимка (default)");

            var balanceSnapshot = BalanceSnapshot.Create(
                traderTelegramId,
                totalBalance,
                mainBalance,
                longOrderReserve,
                shortOrderReserve,
                balanceInTokens,
                snapshotDateTime
            );

            await db.BalanceSnapshots.AddAsync(balanceSnapshot);
            await db.SaveChangesAsync();

            return Ok();
        }, logger, nameof(BalanceSavingService));
    }
}
