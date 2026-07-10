using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Trader;

public class TraderBalanceUpdatingServiceTest
{
    [Fact]
    public async Task Update_NegativeOrZeroAmount_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        await HelpMethods.RegisterTrader(db, 101);
        var result = await HelpMethods.GiveMoney(db, 101, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Сумма должна составлять больше 0", result.Message);
    }

    [Fact]
    public async Task Update_TraderNotExist_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var result = await HelpMethods.GiveMoney(db, 101, 1000);

        Assert.False(result.IsSuccess);
        Assert.Equal("Трейдера не существует", result.Message);
    }
}
