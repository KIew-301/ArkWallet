using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Portfolio;

public class PortfolioQueryServiceTest
{
    [Fact]
    public async Task TakePortfolio_ValidData_ReturnsArray()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var portfolioQueryService = GetPortfolioQueryService(db);

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ", price: 1000);
        await HelpMethods.CreateToken(db, "YYY", price: 1800);
        await HelpMethods.CreateToken(db, "XXX", price: 800);

        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 12);
        await HelpMethods.AddPortfolio(db, 101, "YYY", 27);
        await HelpMethods.AddPortfolio(db, 101, "XXX", 4);

        await HelpMethods.RemoveToken(db, 101, "ZZZ", 6);
        await HelpMethods.GiveToken(db, 101, "XXX", 4);

        var result = await portfolioQueryService.GetTraderTokensAsync(101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3, data.Length);

        Assert.Equal("ZZZ", data[0].TokenInfo.Symbol);
        Assert.Equal("YYY", data[1].TokenInfo.Symbol);
        Assert.Equal("XXX", data[2].TokenInfo.Symbol);

        Assert.Equal(6, data[0].Quantity);
        Assert.Equal(27, data[1].Quantity);
        Assert.Equal(8, data[2].Quantity);

        Assert.Equal(1000, data[0].AverageBuyPrice);
        Assert.Equal(1800, data[1].AverageBuyPrice);
        Assert.Equal(800, data[2].AverageBuyPrice);

        Assert.Equal(6 * 1000, data[0].BalanceInToken);
        Assert.Equal(27 * 1800, data[1].BalanceInToken);
        Assert.Equal(8 * 800, data[2].BalanceInToken);
    }

    [Fact]
    public async Task TakePortfolio_EmptyPortfolio_ReturnsEmptyArray()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var portfolioQueryService = GetPortfolioQueryService(db);

        await HelpMethods.RegisterTrader(db, 101);

        var result = await portfolioQueryService.GetTraderTokensAsync(101);

        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task TakePortfolio_TraderNotExist_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var portfolioQueryService = GetPortfolioQueryService(db);

        var result = await portfolioQueryService.GetTraderTokensAsync(101);

        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    private PortfolioQueryService GetPortfolioQueryService(ArkWalletDbContext db)
    {
        var logger = NullLogger<PortfolioQueryService>.Instance;
        var portfolioQueryService = new PortfolioQueryService(db, logger);
        return portfolioQueryService;
    }
}