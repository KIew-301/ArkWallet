using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.PerformanceTests;

public class SkeletonSmokeTests
{
    [Fact]
    public void Internals_AreVisibleFromPerfProject()
    {
        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new ArkWalletDbContext(options);
        Assert.NotNull(db);
    }
}
