using ArkWallet.Application.Common;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Domain.PortfolioContext;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Portfolio;

public class PortfolioContextChangePositionTests
{
    private static async Task<PortfolioUpdatingService> InitAsync(ArkWalletDbContext db)
    {
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.CreateToken(db, "ZZZ", price: 100m);
        return new PortfolioUpdatingService(db, NullLogger<PortfolioUpdatingService>.Instance);
    }

    [Fact]
    public async Task ChangePosition_Buy_RecalculatesAverageBuyPrice()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Buy, 10, 150m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(20, p.Quantity);
        Assert.Equal(125m, p.AverageBuyPrice);
    }

    [Fact]
    public async Task ChangePosition_Add_IncreasesQuantity_KeepsAverageBuy()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Add, 5, 0m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(15, p.Quantity);
        Assert.Equal(100m, p.AverageBuyPrice);
    }

    [Fact]
    public async Task ChangePosition_Reserve_MovesToReserve()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Reserve, 4, 100m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(6, p.Quantity);
        Assert.Equal(4, p.ReserveQuantity);
    }

    [Fact]
    public async Task ChangePosition_Return_MovesBackToAvailable()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);
        await service.ChangePositionAsync(new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Reserve, 4, 100m));

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Return, 4, 0m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(10, p.Quantity);
        Assert.Equal(0, p.ReserveQuantity);
    }

    [Fact]
    public async Task ChangePosition_Remove_RemovesQuantity()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Remove, 4, 100m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(6, p.Quantity);
    }

    [Fact]
    public async Task ChangePosition_UnknownType_ReturnsFail()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", (PortfolioChangeType)999, 1, 100m));

        Assert.False(result.IsSuccess);
        Assert.Equal("Неизвестная операция над портфелем", result.Message);
    }

    [Fact]
    public async Task ChangePosition_NoExistingPosition_ReturnsFail()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Remove, 1, 100m));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ChangePosition_BuyWithNoExistingPosition_CreatesPosition()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Buy, 10, 100m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Equal(10, p.Quantity);
    }

    [Fact]
    public async Task ChangePosition_RemoveAll_RemovesPositionRow()
    {
        using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = await InitAsync(db);
        await HelpMethods.AddPortfolio(db, 2002, "ZZZ", 10);

        var result = await service.ChangePositionAsync(
            new PortfolioChangeCommand(2002, "ZZZ", PortfolioChangeType.Remove, 10, 100m));

        Assert.True(result.IsSuccess);
        var p = await HelpMethods.GetPortfolio(db, 2002);
        Assert.Null(p);
    }
}
