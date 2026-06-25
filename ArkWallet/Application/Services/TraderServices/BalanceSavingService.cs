using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;
using static Result;

internal class BalanceSavingService(ArkWalletDbContext db, ILogger<BalanceSavingService> logger)
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
        try
        {
            if (snapshotDateTime == default)
                return Fail($"Некорретная дата и время снимка (default)");

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
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка сохранения баланса в истории");
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return Fail($"Внутренняя ошибка сервера: {innerMessage}");
        }
    }
}
