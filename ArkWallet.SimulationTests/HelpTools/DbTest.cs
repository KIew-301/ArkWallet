using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.SimulationTests.HelpTools
{
    internal class DbTest
    {
        internal static ArkWalletDbContext CreateDbContext()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
                .UseSqlite(connection)
                .Options;

            return new ArkWalletDbContext(options);
        }

        internal static async Task<ArkWalletDbContext> CreateInitializedDbContextAsync()
        {
            var db = CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            return db;
        }
    }
}
