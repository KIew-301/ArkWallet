using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace ArkWallet.Tests
{
    internal class DbTest
    {
        private static SqliteConnection _connection;

        internal static ArkWalletDbContext CreateDbContext()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
                .UseSqlite(connection)
                .Options;

            return new ArkWalletDbContext(options);
        }

        internal static void InitTest(string testName)
        {
            var dbFile = $"{testName}.db";
            if (File.Exists(dbFile)) File.Delete(dbFile);

            using var db = CreateHardDbContext(testName);

            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            db.Database.EnsureCreated();
        }

        internal static ArkWalletDbContext CreateHardDbContext(string testName)
        {
            var connectionString = $"Data Source={testName}.db;Cache=Shared";
            var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new ArkWalletDbContext(options);
        }
    }
}
