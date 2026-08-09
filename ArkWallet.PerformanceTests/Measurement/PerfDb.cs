using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class PerfDb
{
    internal static ArkWalletDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options;

        return new ArkWalletDbContext(options);
    }
}
