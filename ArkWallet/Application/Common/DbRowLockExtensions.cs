using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Common;

internal static class DbRowLockExtensions
{
    internal static async Task LockTradersAsync(this ArkWalletDbContext dbContext, IEnumerable<long> telegramIds)
    {
        var ids = telegramIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            return;

        await LockRowsAsync(dbContext, "trader",
            $"SELECT \"TelegramId\" FROM \"Traders\" WHERE \"TelegramId\" = ANY({ids}) ORDER BY \"TelegramId\" FOR UPDATE");
    }

    internal static async Task LockTokenAsync(this ArkWalletDbContext dbContext, string symbol)
        => await LockRowsAsync(dbContext, "token",
            $"SELECT \"Symbol\" FROM \"CharacterTokens\" WHERE \"Symbol\" = {symbol} FOR UPDATE");

    internal static async Task LockMiningMachinesAsync(this ArkWalletDbContext dbContext, IEnumerable<long> machineIds)
    {
        var ids = machineIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            return;

        await LockRowsAsync(dbContext, "mining_machine",
            $"SELECT \"Id\" FROM \"MiningMachines\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE");
    }

    internal static async Task LockMiningMachineSlotsAsync(this ArkWalletDbContext dbContext, IEnumerable<long> slotIds)
    {
        var ids = slotIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            return;

        await LockRowsAsync(dbContext, "mining_slot",
            $"SELECT \"Id\" FROM \"MiningMachineSlots\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE");
    }

    internal static async Task LockActiveMiningMachineSlotsAsync(this ArkWalletDbContext dbContext)
        => await LockRowsAsync(dbContext, "mining_slot",
            $"SELECT \"Id\" FROM \"MiningMachineSlots\" WHERE \"Status\" = 'Active' FOR UPDATE");

    internal static async Task LockSwitchingMiningMachineSlotsAsync(this ArkWalletDbContext dbContext)
        => await LockRowsAsync(dbContext, "mining_slot",
            $"SELECT \"Id\" FROM \"MiningMachineSlots\" WHERE \"Status\" = 'Switching' FOR UPDATE");

    internal static async Task LockMiningMachineRulesAsync(this ArkWalletDbContext dbContext, IEnumerable<long> ruleIds)
    {
        var ids = ruleIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            return;

        await LockRowsAsync(dbContext, "mining_machine_rule",
            $"SELECT \"Id\" FROM \"MiningMachineRules\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE");
    }

    internal static async Task LockMiningGlobalRulesAsync(this ArkWalletDbContext dbContext)
        => await LockRowsAsync(dbContext, "mining_global_rule",
            $"SELECT \"Id\" FROM \"MiningGlobalRules\" FOR UPDATE");

    internal static async Task LockMiningGlobalRuleAsync(this ArkWalletDbContext dbContext, string symbol)
        => await LockRowsAsync(dbContext, "mining_global_rule",
            $"SELECT \"Id\" FROM \"MiningGlobalRules\" WHERE \"TokenId\" = {symbol} FOR UPDATE");

    private static async Task LockRowsAsync(ArkWalletDbContext dbContext, string metric, FormattableString sql)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(sql);
        stopwatch.Stop();

        ArkWalletMetrics.RecordLockWait(metric, stopwatch.Elapsed.TotalSeconds);
    }
}
