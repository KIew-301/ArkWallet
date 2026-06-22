using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Tests
{
    public class BalanceSnapshotServiceTest
    {
        [Fact]
        public async Task BalanceSnapshot_TakeSnaphot_ReturnsSuccess()
        {
            using var db = DbTest.CreateDbContext();
            db.Database.EnsureCreated();

            await HelpMethods.RegisterTrader(db, 101);
            await HelpMethods.RegisterTrader(db, 102);
            await HelpMethods.CreateToken(db, "ZZZ");
            await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

            var resultShortOrder1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 60);
            var resultShortOrder2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 80);
            var resultShortOrder3 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
            var resultLongOrder1= await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 10, 50);
            var resultLongOrder2 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 10, 90);

            var resultSnapshot = await HelpMethods.TakeBalanceSnaphot(db, 102);

            Assert.True(resultSnapshot.IsSuccess);
            Assert.Equal(3300, resultSnapshot.totalBalance);
            Assert.Equal(1200, resultSnapshot.mainBalance);
            Assert.Equal(500, resultSnapshot.longOrderReserve);
            Assert.Equal(400, resultSnapshot.shortOrderReserve);
            Assert.Equal(1200, resultSnapshot.balanceInTokens);
        }
    }
}
