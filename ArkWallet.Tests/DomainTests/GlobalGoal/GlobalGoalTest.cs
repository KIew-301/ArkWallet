using ArkWallet.Domain.GlobalGoalContext;
using ArkWallet.Tests.HelpTools;
using Goal = ArkWallet.Domain.GlobalGoalContext.GlobalGoal;

namespace ArkWallet.Tests.DomainTests.GlobalGoals;

public class GlobalGoalTest
{
    private static readonly TestTimeProvider Time = new()
    {
        DateTimeOffsetNow = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)
    };

    [Fact]
    public void Create_SetsInitialState()
    {
        var goal = Goal.Create(1, "Goal", "Desc", 1000m, 500m);

        Assert.Equal(1, goal.Id);
        Assert.Equal("Goal", goal.Name);
        Assert.Equal("Desc", goal.Description);
        Assert.Equal(1000m, goal.Target);
        Assert.Equal(500m, goal.Actual);
        Assert.Equal(0.5m, goal.Progress);
        Assert.Equal(0, goal.AchievedCount);
        Assert.Empty(goal.History);
        Assert.Empty(goal.Steps);
    }

    [Fact]
    public void Load_RestoresHistoryAndSteps()
    {
        var when = Time.GetUtcNow().UtcDateTime;
        var goal = GoalWith(achievedCount: 3, steps: new()
        {
            new GlobalGoalStep(1, 1500m, "ZZZ", 5m)
        }, history: new()
        {
            new GlobalGoalHistory(when, 1000m, "ZZZ", 5m)
        });

        Assert.Equal(2, goal.Id);
        Assert.Equal(3, goal.AchievedCount);
        Assert.Single(goal.History);
        Assert.Single(goal.Steps);
    }

    [Fact]
    public void UpdateActual_RefreshesProgress()
    {
        var goal = Goal.Create(1, "Goal", "Desc", 1000m, 500m);
        goal.UpdateActual(1000m);

        Assert.Equal(1000m, goal.Actual);
        Assert.Equal(1m, goal.Progress);
    }

    [Fact]
    public async Task CheckGoal_NotAchieved_DoesNothing()
    {
        var goal = Goal.Create(1, "Goal", "Desc", 1000m, 900m);
        goal.SetEventPublisher(new RecordingEventPublisher());

        await goal.CheckGoal(Time);

        Assert.Equal(0, goal.AchievedCount);
        Assert.Empty(goal.History);
    }

    [Fact]
    public async Task CheckGoal_Achieved_PublishesEventAndMovesToNextStep()
    {
        var publisher = new RecordingEventPublisher();
        var goal = GoalWith(steps: new()
        {
            new GlobalGoalStep(1, 1500m, "ZZZ", 10m)
        });
        goal.SetEventPublisher(publisher);

        await goal.CheckGoal(Time);

        Assert.Equal(1, goal.AchievedCount);
        var entry = Assert.Single(goal.History);
        Assert.Equal(Time.GetUtcNow().UtcDateTime, entry.AchievedAt);
        Assert.Equal(1000m, entry.Target);
        Assert.Equal("ZZZ", entry.SymbolForReward);
        Assert.Equal(10m, entry.AmountForReward);
        Assert.Equal(1500m, goal.Target);

        var evt = Assert.IsType<GlobalGoalAchievedEvent>(Assert.Single(publisher.Events));
        Assert.Equal("Goal", evt.GoalName);
        Assert.Equal("ZZZ", evt.SymbolForReward);
        Assert.Equal(10m, evt.AmountForReward);
    }

    [Fact]
    public async Task CheckGoal_Achieved_NoStep_UsesEmptyRewardAndKeepsTarget()
    {
        var publisher = new RecordingEventPublisher();
        var goal = Goal.Create(1, "Goal", "Desc", 1000m, 1000m);
        goal.SetEventPublisher(publisher);

        await goal.CheckGoal(Time);

        Assert.Equal(1, goal.AchievedCount);
        var entry = Assert.Single(goal.History);
        Assert.Equal(string.Empty, entry.SymbolForReward);
        Assert.Equal(0m, entry.AmountForReward);
        Assert.Equal(1000m, goal.Target);
        Assert.Single(publisher.Events);
    }

    [Fact]
    public async Task CheckGoal_MultipleAchievements_UsesNextSteps()
    {
        var publisher = new RecordingEventPublisher();
        var goal = GoalWith(steps: new()
        {
            new GlobalGoalStep(1, 1000m, "AAA", 5m),
            new GlobalGoalStep(2, 2000m, "BBB", 10m)
        });
        goal.SetEventPublisher(publisher);

        await goal.CheckGoal(Time);
        goal.UpdateActual(2000m);
        await goal.CheckGoal(Time);

        Assert.Equal(2, goal.AchievedCount);
        Assert.Equal(2, goal.History.Count);
        Assert.Equal("BBB", goal.History[1].SymbolForReward);
        Assert.Equal(2000m, goal.Target);
        Assert.Equal(2, publisher.Events.Count);
    }

    [Fact]
    public async Task CheckGoal_WithoutPublisher_Throws()
    {
        var goal = Goal.Create(1, "Goal", "Desc", 1000m, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => goal.CheckGoal(Time));
    }

    private static Goal GoalWith(
        int achievedCount = 0,
        List<GlobalGoalStep>? steps = null,
        List<GlobalGoalHistory>? history = null)
        => Goal.Load(new GlobalGoalData(
            Id: 2,
            Name: "Goal",
            Description: "Desc",
            Target: 1000m,
            Actual: 1000m,
            AchievedCount: achievedCount,
            History: history ?? new(),
            Steps: steps ?? new()));
}
