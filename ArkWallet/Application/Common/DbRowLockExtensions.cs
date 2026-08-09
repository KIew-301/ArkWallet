using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Common;

internal static class DbRowLockExtensions
{
    internal static async Task LockTradersAsync(this ArkWalletDbContext dbContext, IEnumerable<long> telegramIds)
    {
        var ids = telegramIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0 || !dbContext.Database.IsNpgsql())
            return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"TelegramId\" FROM \"Traders\" WHERE \"TelegramId\" = ANY({ids}) ORDER BY \"TelegramId\" FOR UPDATE");
        stopwatch.Stop();

        ArkWalletMetrics.RecordLockWait("trader", stopwatch.Elapsed.TotalSeconds);
    }

    internal static async Task LockTokenAsync(this ArkWalletDbContext dbContext, string symbol)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Symbol\" FROM \"CharacterTokens\" WHERE \"Symbol\" = {symbol} FOR UPDATE");
        stopwatch.Stop();

        ArkWalletMetrics.RecordLockWait("token", stopwatch.Elapsed.TotalSeconds);
    }
}
