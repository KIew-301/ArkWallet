using ArkWallet.Infrastructure.Data;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class PerfWarmup
{
    public static async Task RunAsync(Func<Task> action)
        => await action();

    public static async Task WithDbAsync(Func<ArkWalletDbContext, Task> action)
    {
        using var db = PerfDb.CreateDbContext(new QueryCounter());
        await db.Database.EnsureCreatedAsync();
        await action(db);
    }
}
