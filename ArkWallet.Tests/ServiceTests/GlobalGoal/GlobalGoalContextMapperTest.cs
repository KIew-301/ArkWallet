using ArkWallet.Application.Services.GlobalGoalServices;
using ArkWallet.Domain.Entities;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Tests.ServiceTests.GlobalGoals;

public class GlobalGoalContextMapperTest
{
    [Fact]
    public void ToGoals_MapsEntitiesToAggregates()
    {
        var record = Records.GlobalGoal.Create(1, "Goal", "Desc", 1000m, 2000m, 2m, 4);
        record.Histories.Add(Records.GlobalGoalHistory.Create(1, new DateTime(2026, 1, 1), 1000m, "ZZZ", 5m));
        record.Steps.Add(Records.GlobalGoalStep.Create(1, 1, 1500m, "YYY", 10m));

        var goals = GlobalGoalContextMapper.ToGoals(new() { record });

        var goal = Assert.Single(goals);
        Assert.Equal(1, goal.Id);
        Assert.Equal("Goal", goal.Name);
        Assert.Equal(4, goal.AchievedCount);
        var history = Assert.Single(goal.History);
        Assert.Equal("ZZZ", history.SymbolForReward);
        var step = Assert.Single(goal.Steps);
        Assert.Equal(1500m, step.Target);
    }

    [Fact]
    public void ToGoals_NullCollections_DoesNotThrow()
    {
        var record = Records.GlobalGoal.Create(1, "Goal", "Desc", 1000m, 2000m, 2m, 4);
        record.Histories = null!;
        record.Steps = null!;

        var goals = GlobalGoalContextMapper.ToGoals(new() { record });

        var goal = Assert.Single(goals);
        Assert.Empty(goal.History);
        Assert.Empty(goal.Steps);
    }

    [Fact]
    public void ToGoals_EmptyList_ReturnsEmpty()
    {
        var goals = GlobalGoalContextMapper.ToGoals(new());

        Assert.Empty(goals);
    }
}
