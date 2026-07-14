using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Portfolio;

public class PortfolioUpdatingServiceEdgeCaseTests
{
    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_ZeroQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new PortfolioUpdatingService(db);

        var result = await service.CreateOrUpdatePortfolioAsync(101, "ZZZ", 0);

        Assert.False(result.IsSuccess);
        Assert.Contains("минимум один токен", result.Message);
    }

    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_NegativeQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new PortfolioUpdatingService(db);

        var result = await service.CreateOrUpdatePortfolioAsync(101, "ZZZ", -5);

        Assert.False(result.IsSuccess);
        Assert.Contains("минимум один токен", result.Message);
    }
}
