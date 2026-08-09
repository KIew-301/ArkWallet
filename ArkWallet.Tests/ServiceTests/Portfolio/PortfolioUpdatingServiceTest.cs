using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Portfolio;

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

    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_TokenNotExist_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);

        var service = new PortfolioUpdatingService(db, NullLogger<PortfolioUpdatingService>.Instance);
        var result = await service.CreateOrUpdatePortfolioAsync(101, "NONEXISTENT", 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("Токен", result.Message);
    }
}