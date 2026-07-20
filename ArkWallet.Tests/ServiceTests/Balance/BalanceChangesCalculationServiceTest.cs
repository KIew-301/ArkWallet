using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Balance;

public class BalanceChangesCalculationServiceTest
{
    [Fact]
    public async Task MainBalanceCalculationChanges_NoChangesWithoutBalanceHistoryEntry_ReturnSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 101);
        var result = await calculationService.TakeMainBalanceChanges(101, 1);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1000m, data.CurrentBalance);
        Assert.Equal(1000m, data.PreviousBalance);
        Assert.Equal(0m, data.ChangeAbsolute);
        Assert.Equal(0m, data.ChangePercent);
    }

    [Fact]
    public async Task MainBalanceCalculationChanges_WithChangesWithoutBalanceHistoryEntry_ReturnSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.GiveMoney(db, 101, 2500);

        var result = await calculationService.TakeMainBalanceChanges(101, 1);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3500m, data.CurrentBalance);
        Assert.Equal(1000m, data.PreviousBalance);
        Assert.Equal(2500m, data.ChangeAbsolute);
        Assert.Equal(250m, data.ChangePercent);
    }

    [Theory]
    [InlineData(1, 2000, 1500)]
    [InlineData(2, 2000, 2500)]
    [InlineData(3, 2000, 1750)]
    [InlineData(7, 2000, 1000)]
    public async Task MainBalanceCalculationChanges_WithChangesWithBalanceHistoryEntryWithPeriods_ReturnSuccess(
        int period, decimal currentBalance, decimal previousBalance)
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.SaveBalanceSnapshot(db, 101, 1000, 1000, 0, 0, 0, DateTime.UtcNow.Date.AddDays(-7));
        await HelpMethods.SaveBalanceSnapshot(db, 101, 1750, 1750, 0, 0, 0, DateTime.UtcNow.Date.AddDays(-3));
        await HelpMethods.SaveBalanceSnapshot(db, 101, 2500, 2500, 0, 0, 0, DateTime.UtcNow.Date.AddDays(-2));
        await HelpMethods.SaveBalanceSnapshot(db, 101, 1500, 1500, 0, 0, 0, DateTime.UtcNow.Date.AddDays(-1));
        await HelpMethods.GiveMoney(db, 101, 1000);

        var result = await calculationService.TakeMainBalanceChanges(101, period);
        var changeAbsolute = currentBalance - previousBalance;
        var changePercent = changeAbsolute / previousBalance * 100;

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(currentBalance, data.CurrentBalance);
        Assert.Equal(previousBalance, data.PreviousBalance);
        Assert.Equal(changeAbsolute, data.ChangeAbsolute, precision: 2);
        Assert.Equal(changePercent, data.ChangePercent, precision: 2);
    }

    [Fact]
    public async Task MainBalanceCalculationChanges_WithoutPeriodForCalculation_ReturnSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 101);
        var result = await calculationService.TakeMainBalanceChanges(101, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Минимальный период для расчёта: 1 день", result.Message);
    }

    [Fact]
    public async Task MainBalanceCalculationChanges_TakeCurrencySnapshotInvalid_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockSnapshotServiceService = new Mock<IBalanceSnapshotService>();
        mockSnapshotServiceService
            .Setup(x => x.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync(Result<BalanceSnapshotData>.Fail("Error"));

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, mockSnapshotServiceService.Object, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 101);
        var result = await calculationService.TakeMainBalanceChanges(101, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error", result.Message);
    }

    [Fact]
    public async Task TotalBalanceCalculationChanges_NoHistory_ReturnSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 202);
        var result = await calculationService.TakeTotalBalanceChanges(202, 1);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(data.CurrentBalance, data.PreviousBalance);
        Assert.Equal(0m, data.ChangeAbsolute);
        Assert.Equal(0m, data.ChangePercent);
    }

    [Fact]
    public async Task TotalBalanceCalculationChanges_WithHistory_ReturnSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        await HelpMethods.RegisterTrader(db, 202);
        await HelpMethods.SaveBalanceSnapshot(db, 202, 1000, 1000, 0, 0, 0, DateTime.UtcNow.Date.AddDays(-7));
        await HelpMethods.GiveMoney(db, 202, 500);

        var result = await calculationService.TakeTotalBalanceChanges(202, 7);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1500m, data.CurrentBalance);
        Assert.Equal(1000m, data.PreviousBalance);
        Assert.Equal(500m, data.ChangeAbsolute);
    }

    [Fact]
    public async Task TotalBalanceCalculationChanges_InvalidPeriod_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotServicelogger = NullLogger<BalanceSnapshotService>.Instance;
        var snapshotService = new BalanceSnapshotService(db, snapshotServicelogger);

        var calculationServicelogger = NullLogger<BalanceChangesCalculationService>.Instance;
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, calculationServicelogger);

        var result = await calculationService.TakeTotalBalanceChanges(202, 0);

        Assert.False(result.IsSuccess);
    }
}