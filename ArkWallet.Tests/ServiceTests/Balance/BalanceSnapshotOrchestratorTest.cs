using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Balance;

public class BalanceSnapshotOrchestratorTest
{
    [Fact]
    public async Task CreateSnapshots_OneTrader_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var savingService = new BalanceSavingService(db, NullLogger<BalanceSavingService>.Instance);
        var logger = NullLogger<BalanceSnapshotOrchestrator>.Instance;

        var orchestrator = new BalanceSnapshotOrchestrator(db, snapshotService, savingService, logger);
        var result = await orchestrator.CreateSnapshotsForAllTradersAsync();

        Assert.True(result.IsSuccess);

        var history = await HelpMethods.GetBalanceHistory(db, 1001);
        Assert.Single(history);
    }

    [Fact]
    public async Task CreateSnapshots_NoTraders_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var savingService = new BalanceSavingService(db, NullLogger<BalanceSavingService>.Instance);
        var logger = NullLogger<BalanceSnapshotOrchestrator>.Instance;

        var orchestrator = new BalanceSnapshotOrchestrator(db, snapshotService, savingService, logger);
        var result = await orchestrator.CreateSnapshotsForAllTradersAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateSnapshots_SnapshotServiceFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);

        var mockSnapshotService = new Mock<IBalanceSnapshotService>();
        mockSnapshotService
            .Setup(x => x.TakeTotalTraderBalanceSnapshot(1001))
            .ReturnsAsync(Result<BalanceSnapshotData>.Fail("Ошибка создания снимка"));

        var savingService = new BalanceSavingService(db, NullLogger<BalanceSavingService>.Instance);
        var logger = NullLogger<BalanceSnapshotOrchestrator>.Instance;

        var orchestrator = new BalanceSnapshotOrchestrator(db, mockSnapshotService.Object, savingService, logger);
        var result = await orchestrator.CreateSnapshotsForAllTradersAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Ошибка создания снимка", result.Message);
    }

    [Fact]
    public async Task CreateSnapshots_SavingServiceFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);

        var mockSnapshotService = new Mock<IBalanceSnapshotService>();
        mockSnapshotService
            .Setup(x => x.TakeTotalTraderBalanceSnapshot(1001))
            .ReturnsAsync(Result<BalanceSnapshotData>.Ok(
                new BalanceSnapshotData(1001, 2000, 1000, 250, 250, 500, DateTime.UtcNow)));

        var mockSavingService = new Mock<IBalanceSavingService>();
        mockSavingService
            .Setup(x => x.SaveBalanceToDatabase(
                It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Result.Fail("Ошибка сохранения снимка"));

        var logger = NullLogger<BalanceSnapshotOrchestrator>.Instance;

        var orchestrator = new BalanceSnapshotOrchestrator(db, mockSnapshotService.Object, mockSavingService.Object, logger);
        var result = await orchestrator.CreateSnapshotsForAllTradersAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Ошибка сохранения снимка", result.Message);
    }
}
