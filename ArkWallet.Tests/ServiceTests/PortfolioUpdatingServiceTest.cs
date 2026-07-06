using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests;

public class PortfolioUpdatingServiceTest
{
    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_CreateNewPorfolio_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        var portfolio = await HelpMethods.GetPortfolio(db, 101);

        Assert.True(result.IsSuccess);
        Assert.NotNull(portfolio);
        Assert.Equal(100, portfolio.Quantity);
    }

    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_UpdatePorfolioWithDecrease_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        var result = await HelpMethods.AddPortfolio(db, 101, "ZZZ", 80);
        var portfolio = await HelpMethods.GetPortfolio(db, 101);

        Assert.True(result.IsSuccess);
        Assert.NotNull(portfolio);
        Assert.Equal(80, portfolio.Quantity);
    }

    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_UpdatePorfolioWithIncrease_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        var result = await HelpMethods.AddPortfolio(db, 101, "ZZZ", 225);
        var portfolio = await HelpMethods.GetPortfolio(db, 101);

        Assert.True(result.IsSuccess);
        Assert.NotNull(portfolio);
        Assert.Equal(225, portfolio.Quantity);
    }
}