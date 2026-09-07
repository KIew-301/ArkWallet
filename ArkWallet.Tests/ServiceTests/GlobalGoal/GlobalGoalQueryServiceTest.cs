using ArkWallet.Application.Services.GlobalGoalServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.GlobalGoals;

public class GlobalGoalQueryServiceTest
{
    private static ArkWalletDbContext CreateDb()
        => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    [Fact]
    public async Task GetGoalsAsync_ReturnsGoalsOrderedById()
    {
        using var db = CreateDb();
        db.GlobalGoals.Add(GlobalGoal.Create(2, "B", "d", 2000m, 1000m, 0.5m, 0));
        db.GlobalGoals.Add(GlobalGoal.Create(1, "A", "d", 1000m, 1000m, 1m, 1));
        db.GlobalGoalSteps.Add(GlobalGoalStep.Create(1, 1, 1000m, "ZZZ", 5m));
        await db.SaveChangesAsync();

        var service = new GlobalGoalQueryService(db, NullLogger<GlobalGoalQueryService>.Instance);
        var result = await service.GetGoalsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var goals));
        Assert.Equal(2, goals!.Count);
        Assert.Equal("A", goals[0].Name);
        Assert.Equal("B", goals[1].Name);
        Assert.Equal(1, goals[0].AchievedCount);
        var step = Assert.Single(goals[0].Steps);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal("ZZZ", step.SymbolForReward);
    }

    [Fact]
    public async Task GetGoalsAsync_NoGoals_ReturnsEmpty()
    {
        using var db = CreateDb();

        var service = new GlobalGoalQueryService(db, NullLogger<GlobalGoalQueryService>.Instance);
        var result = await service.GetGoalsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var goals));
        Assert.Empty(goals!);
    }
}
