using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

/// <summary>
/// Расчёт цели "Общий баланс": сумма totalBalance самых свежих снимков всех участников
/// сервера, за исключением ботов.
/// </summary>
internal class TotalBalanceGlobalGoalCalculation : IDomainGlobalGoalCalculation
{
    private const long BotTraderIdsMin = 100;
    private const long BotTraderIdsMax = 1000;
    private const int BalanceScale = 4;

    public string GoalName => "Общий баланс";

    /// <remarks>
    /// BalanceSnapshot.TotalBalance is mapped to an unbounded PostgreSQL numeric, so the long
    /// fractional tail of token prices gives each snapshot up to ~29 significant digits. Every
    /// snapshot on its own still fits System.Decimal, but their aggregate does not: on production
    /// the SUM reached 31 digits and Npgsql threw OverflowException while materialising it.
    /// Rounding each balance bounds it to 4 fractional digits, which caps a value at ~24 integer
    /// digits and keeps the total within Decimal capacity. The per-trader balances are summed in
    /// memory because the SQLite provider used by the test suite cannot translate rounding into
    /// an aggregate, and letting the database accumulate the sum reintroduces the overflow.
    /// </remarks>
    public async Task<decimal> CalculateAsync(ArkWalletDbContext dbContext)
    {
        var balances = await dbContext.BalanceSnapshots
            .Where(s => s.TraderId < BotTraderIdsMin || s.TraderId > BotTraderIdsMax)
            .GroupBy(s => s.TraderId)
            .Select(g => g
                .OrderByDescending(s => s.SnapshotDateTime)
                .Select(s => s.TotalBalance)
                .FirstOrDefault())
            .ToListAsync();

        var sum = 0m;
        foreach (var balance in balances)
        {
            sum += Math.Round(balance, BalanceScale);
        }

        return sum;
    }
}
