using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TraderServices;

internal class BalanceSavingService(ArkWalletDbContext db, ILogger<BalanceSavingService> logger)
{
    public async Task<BalanceSavingResult> SaveBalanceToDatabase(long traderTelegramId,
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
                return BalanceSavingResult.Fail($"Некорретная дата и время снимка (default)");

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

            return BalanceSavingResult.Ok();
        }
        catch (DomainException ex)
        {
            return BalanceSavingResult.Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка сохранения баланса в истории");
            return BalanceSavingResult.Fail("Внутренняя ошибка сервера");
        }
    }
}

internal record BalanceSavingResult(
    bool IsSuccess, string message)
{
    public static BalanceSavingResult Ok()
    {
        return new(true, "Данные о баланса сохранены в историю");
    }

    public static BalanceSavingResult Fail(string message)
    {
        return new(false, message);
    }
};
