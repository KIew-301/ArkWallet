using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class PerfDb
{
    internal static ArkWalletDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        return RepeatConfig.DatabaseProvider switch
        {
            "postgres" or "postgresql" => CreatePostgresDbContext(interceptors),
            _ => CreateSqliteDbContext(interceptors),
        };
    }

    private static ArkWalletDbContext CreateSqliteDbContext(IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options;

        return new ArkWalletDbContext(options);
    }

    private static ArkWalletDbContext CreatePostgresDbContext(IInterceptor[] interceptors)
    {
        var connectionString = PostgresPerfContainer.GetConnectionStringAsync()
            .GetAwaiter().GetResult();

        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptors)
            .Options;

        var context = new ArkWalletDbContext(options);
        context.Database.EnsureDeleted();
        return context;
    }
}
