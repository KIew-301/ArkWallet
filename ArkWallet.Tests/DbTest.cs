using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArkWallet.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests
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
    }
}
