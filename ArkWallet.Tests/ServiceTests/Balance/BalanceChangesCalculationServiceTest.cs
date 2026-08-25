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

        await HelpMethods.RegisterTrader(db, 1001);
        var result = await calculationService.TakeMainBalanceChanges(1001, 1);

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

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.GiveMoney(db, 1001, 2500);

        var result = await calculationService.TakeMainBalanceChanges(1001, 1);

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

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.SaveBalanceSnapshot(db, 1001, 1000, 1000, 0, 0, 0, DateTime.UtcNow.AddDays(-7));
        await HelpMethods.SaveBalanceSnapshot(db, 1001, 1750, 1750, 0, 0, 0, DateTime.UtcNow.AddDays(-3));
        await HelpMethods.SaveBalanceSnapshot(db, 1001, 2500, 2500, 0, 0, 0, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.SaveBalanceSnapshot(db, 1001, 1500, 1500, 0, 0, 0, DateTime.UtcNow.AddDays(-1));
        await HelpMethods.GiveMoney(db, 1001, 1000);

        var result = await calculationService.TakeMainBalanceChanges(1001, period);
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

        await HelpMethods.RegisterTrader(db, 1001);
        var result = await calculationService.TakeMainBalanceChanges(1001, 0);

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

        await HelpMethods.RegisterTrader(db, 1001);
        var result = await calculationService.TakeMainBalanceChanges(1001, 1);

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

        await HelpMethods.RegisterTrader(db, 1202);
        var result = await calculationService.TakeTotalBalanceChanges(1202, 1);

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

        await HelpMethods.RegisterTrader(db, 1202);
        await HelpMethods.SaveBalanceSnapshot(db, 1202, 1000, 1000, 0, 0, 0, DateTime.UtcNow.AddDays(-7));
        await HelpMethods.GiveMoney(db, 1202, 500);

        var result = await calculationService.TakeTotalBalanceChanges(1202, 7);

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

        var result = await calculationService.TakeTotalBalanceChanges(1202, 0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TakeBalanceChanges_ValidPeriod_ReturnsBundle()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, NullLogger<BalanceChangesCalculationService>.Instance);

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.SaveBalanceSnapshot(db, 1001, 1000, 1000, 0, 0, 0, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.GiveMoney(db, 1001, 500);

        var result = await calculationService.TakeBalanceChanges(1001, 1);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1500m, data.Main.CurrentBalance);
        Assert.Equal(1000m, data.Main.PreviousBalance);
        Assert.Equal(1500m, data.Total.CurrentBalance);
    }

    [Fact]
    public async Task TakeBalanceChanges_InvalidPeriod_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var calculationService = new BalanceChangesCalculationService(db, snapshotService, NullLogger<BalanceChangesCalculationService>.Instance);

        var result = await calculationService.TakeBalanceChanges(1001, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Минимальный период для расчёта: 1 день", result.Message);
    }

    [Fact]
    public async Task TakeBalanceChanges_SnapshotFails_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockSnapshotService = new Mock<IBalanceSnapshotService>();
        mockSnapshotService
            .Setup(x => x.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync(Result<BalanceSnapshotData>.Fail("Error"));

        var calculationService = new BalanceChangesCalculationService(db, mockSnapshotService.Object, NullLogger<BalanceChangesCalculationService>.Instance);

        var result = await calculationService.TakeBalanceChanges(1001, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error", result.Message);
    }
}