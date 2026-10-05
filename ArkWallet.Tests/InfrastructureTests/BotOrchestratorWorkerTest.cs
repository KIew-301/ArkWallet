using System.Text.Json;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Workers;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.InfrastructureTests;

public sealed class BotOrchestratorWorkerTest : IDisposable
{
    private readonly List<IAsyncDisposable> _disposables = [];

    [Fact]
    public async Task ScheduledJob_PastDue_ExecutesAndUpdatesNextRun()
    {
        // Arrange
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        var futureTime = DateTime.UtcNow.AddHours(1);

        await db.AppStates.AddAsync(AppState.Create("BotGridsNextExecution", pastTime));
        await db.AppStates.AddAsync(AppState.Create("BotPowerNextExecution", futureTime));
        await db.AppStates.AddAsync(AppState.Create("BotMarketNextExecution", futureTime));
        await db.AppStates.AddAsync(AppState.Create("BotBalancesNextExecution", futureTime));
        await db.AppStates.AddAsync(AppState.Create("BotWallNextExecution", futureTime));
        await db.SaveChangesAsync();

        // Act
        await worker.RunScheduledJobsAsync(CancellationToken.None);

        // Assert — mock calls
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Never);

        // Assert — DB update
        var stateRecord = await db.AppStates.FindAsync(["BotGridsNextExecution"], CancellationToken.None);
        Assert.NotNull(stateRecord);
        var newValue = JsonSerializer.Deserialize<DateTime?>(stateRecord.Value);
        Assert.NotNull(newValue);
        Assert.True(newValue!.Value > pastTime, $"Updated value {newValue.Value:o} should be after {pastTime:o}, got {stateRecord.Value}");
    }

    [Fact]
    public async Task ScheduledJob_FutureDue_SkipsExecution()
    {
        // Arrange
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        var futureTime = DateTime.UtcNow.AddHours(1);
        foreach (var key in new[]
        {
            "BotBalancesNextExecution",
            "BotGridsNextExecution",
            "BotPowerNextExecution",
            "BotMarketNextExecution",
            "BotWallNextExecution"
        })
        {
            await db.AppStates.AddAsync(AppState.Create(key, futureTime));
        }
        await db.SaveChangesAsync();

        // Act
        await worker.RunScheduledJobsAsync(CancellationToken.None);

        // Assert
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Never);

        var stateRecord = await db.AppStates.FindAsync(["BotGridsNextExecution"], CancellationToken.None);
        var currentValue = JsonSerializer.Deserialize<DateTime?>(stateRecord!.Value);
        Assert.Equal(futureTime, currentValue);
    }

    [Fact]
    public async Task NoAppState_CreatesAndRunsAllJobs()
    {
        // Arrange
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        // Act — no states seeded
        await worker.RunScheduledJobsAsync(CancellationToken.None);

        // Assert — missing state is treated as due and a future next-run is created
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Once);

        foreach (var key in new[]
        {
            "BotBalancesNextExecution",
            "BotGridsNextExecution",
            "BotPowerNextExecution",
            "BotMarketNextExecution",
            "BotWallNextExecution"
        })
        {
            var stateRecord = await db.AppStates.FindAsync([key], CancellationToken.None);
            Assert.NotNull(stateRecord);
            var nextValue = JsonSerializer.Deserialize<DateTime?>(stateRecord.Value);
            Assert.NotNull(nextValue);
            Assert.True(nextValue!.Value > DateTime.UtcNow, $"{key} next run should be scheduled in the future");
        }
    }

    [Fact]
    public async Task FailingJob_DoesNotBlockRestOfSchedule()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var orchMock = new Mock<IBotOrchestrator>();
        // Grids fails the way it does in production: not enough balance on the bots.
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Insufficient balance"));
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        await worker.RunScheduledJobsAsync(CancellationToken.None);

        // The balance job funds the bots, and the wall job cancels stale grid orders.
        // Neither may be skipped just because an unrelated job failed.
        orchMock.Verify(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Once);

        foreach (var key in new[]
        {
            "BotBalancesNextExecution",
            "BotGridsNextExecution",
            "BotPowerNextExecution",
            "BotMarketNextExecution",
            "BotWallNextExecution"
        })
        {
            var stateRecord = await db.AppStates.FindAsync([key], CancellationToken.None);
            Assert.NotNull(stateRecord);
            Assert.NotNull(JsonSerializer.Deserialize<DateTime?>(stateRecord.Value));
        }
    }

    [Fact]
    public async Task BalancesRunBeforeGrids()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var callOrder = new List<string>();
        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { callOrder.Add("balances"); return Result.Ok(); });
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { callOrder.Add("grids"); return Result.Ok(); });
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        await worker.RunScheduledJobsAsync(CancellationToken.None);

        Assert.Equal(new[] { "balances", "grids" }, callOrder);
    }

    [Fact]
    public async Task MultiplePastDue_ExecutesAll()
    {
        // Arrange
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<ArkWalletDbContext>(_ => db);

        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        foreach (var key in new[]
        {
            "BotBalancesNextExecution",
            "BotGridsNextExecution",
            "BotPowerNextExecution",
            "BotMarketNextExecution",
            "BotWallNextExecution"
        })
        {
            await db.AppStates.AddAsync(AppState.Create(key, pastTime));
        }
        await db.SaveChangesAsync();

        // Act
        await worker.RunScheduledJobsAsync(CancellationToken.None);

        // Assert
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.ExecuteMarketOrdersAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateAllBotsBalancesAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureBotsAndRefresh_Changed_RefreshesGridsAndPowers()
    {
        // Arrange
        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BotEnsuringResult>.Ok(new BotEnsuringResult(true, 2, 1)));
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        var services = new ServiceCollection();
        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        // Act
        await worker.EnsureBotsAndRefreshAsync(CancellationToken.None);

        // Assert
        orchMock.Verify(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureBotsAndRefresh_NotChanged_DoesNotRefresh()
    {
        // Arrange
        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BotEnsuringResult>.Ok(new BotEnsuringResult(false, 0, 0)));
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        var services = new ServiceCollection();
        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        // Act
        await worker.EnsureBotsAndRefreshAsync(CancellationToken.None);

        // Assert
        orchMock.Verify(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureBotsAndRefresh_EnsureFails_DoesNotRefresh()
    {
        // Arrange
        var orchMock = new Mock<IBotOrchestrator>();
        orchMock.Setup(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BotEnsuringResult>.Fail("boom"));
        orchMock.Setup(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        orchMock.Setup(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());

        var services = new ServiceCollection();
        services.AddScoped<IBotOrchestrator>(_ => orchMock.Object);
        var provider = services.BuildServiceProvider();

        var worker = new BotOrchestratorWorker(provider, NullLogger<BotOrchestratorWorker>.Instance);

        // Act — не должен бросить
        await worker.EnsureBotsAndRefreshAsync(CancellationToken.None);

        // Assert
        orchMock.Verify(o => o.EnsureDefaultBotsAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchMock.Verify(o => o.UpdateBotsGridsAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.UpdateWallBotGridsAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchMock.Verify(o => o.RebalanceAllBotsPowerAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
