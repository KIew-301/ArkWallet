using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Application.Services.GlobalGoalServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.GlobalGoals;

public class GlobalGoalCheckingServiceTest
{
    private static ArkWalletDbContext CreateDb()
        => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    private static GlobalGoalCheckingService BuildService(ArkWalletDbContext db, params IDomainGlobalGoalCalculation[] calculations)
        => new(db, calculations, new RecordingEventPublisher(), NullLogger<GlobalGoalCheckingService>.Instance, new TestTimeProvider());

    [Fact]
    public async Task CheckGoalsAsync_NoGoal_ReturnsOk()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CheckGoalsAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckGoalsAsync_NotAchieved_NoHistoryNoEvent()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(
            id: 1, name: "Goal", description: "d", target: 1000m, actual: 0m, progress: 0m, achievedCount: 0));
        await db.SaveChangesAsync();

        var service = BuildService(db, new FakeCalculation("Goal", 500m));
        var result = await service.CheckGoalsAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(db.GlobalGoalHistories.ToList());
        Assert.Equal(500m, db.GlobalGoals.Single().Actual);
    }

    [Fact]
    public async Task CheckGoalsAsync_Achieved_AddsHistoryAndPersists()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "Goal", "d", 1000m, 0m, 0m, 0));
        db.GlobalGoalSteps.Add(GlobalGoalStep.Create(1, 1, 1500m, "ZZZ", 10m));
        await db.SaveChangesAsync();

        var service = BuildService(db, new FakeCalculation("Goal", 1200m));
        var result = await service.CheckGoalsAsync();

        Assert.True(result.IsSuccess);
        var record = db.GlobalGoals.Single();
        Assert.Equal(1, record.AchievedCount);
        Assert.Equal(1200m, record.Actual);
        var history = Assert.Single(db.GlobalGoalHistories.ToList());
        Assert.Equal(1, history.GoalId);
        Assert.Equal("ZZZ", history.SymbolForReward);
    }

    [Fact]
    public async Task CheckGoalsAsync_NoMatchingCalculation_KeepsActual()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "OtherGoal", "d", 1000m, 100m, 0m, 0));
        await db.SaveChangesAsync();

        var service = BuildService(db, new FakeCalculation("Goal", 1200m));
        var result = await service.CheckGoalsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, db.GlobalGoals.Single().Actual);
        Assert.Empty(db.GlobalGoalHistories.ToList());
    }

    [Fact]
    public async Task CheckGoalsAsync_MultipleGoals_EachEvaluated()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(1, "GoalA", "d", 1000m, 0m, 0m, 0));
        db.GlobalGoalSteps.Add(GlobalGoalStep.Create(1, 1, 2000m, "AAA", 5m));
        db.GlobalGoals.Add(GlobalGoal.Create(2, "GoalB", "d", 1000m, 0m, 0m, 0));
        db.GlobalGoalSteps.Add(GlobalGoalStep.Create(2, 1, 2000m, "BBB", 7m));
        await db.SaveChangesAsync();

        var service = BuildService(
            db,
            new FakeCalculation("GoalA", 900m),
            new FakeCalculation("GoalB", 1500m));
        var result = await service.CheckGoalsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(900m, db.GlobalGoals.Single(g => g.Id == 1).Actual);
        Assert.Equal(1, db.GlobalGoals.Single(g => g.Id == 2).AchievedCount);
        Assert.Single(db.GlobalGoalHistories.ToList());
    }

    private sealed class FakeCalculation(string goalName, decimal value) : IDomainGlobalGoalCalculation
    {
        public string GoalName => goalName;
        public Task<decimal> CalculateAsync(ArkWalletDbContext dbContext) => Task.FromResult(value);
    }
}
