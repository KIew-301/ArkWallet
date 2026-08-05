using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class PerfDb
{
    internal static ArkWalletDbContext CreateDbContext(QueryCounter counter)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        return new ArkWalletDbContext(options);
    }
}
