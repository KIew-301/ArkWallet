using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.GlobalGoalServices;

/// <summary>
/// Расчёт цели "Общий баланс": сумма totalBalance самых свежих снимков всех участников
/// сервера, за исключением ботов.
/// </summary>
internal class TotalBalanceGlobalGoalCalculation : IDomainGlobalGoalCalculation
{
    private const long BotTraderIdsMin = 100;
    private const long BotTraderIdsMax = 1000;

    public string GoalName => "Общий баланс";

    public async Task<decimal> CalculateAsync(ArkWalletDbContext dbContext)
    {
        var sum = await dbContext.BalanceSnapshots
            .Where(s => s.TraderId < BotTraderIdsMin || s.TraderId > BotTraderIdsMax)
            .GroupBy(s => s.TraderId)
            .Select(g => g
                .OrderByDescending(s => s.SnapshotDateTime)
                .Select(s => s.TotalBalance)
                .FirstOrDefault())
            .SumAsync();

        return sum;
    }
}
