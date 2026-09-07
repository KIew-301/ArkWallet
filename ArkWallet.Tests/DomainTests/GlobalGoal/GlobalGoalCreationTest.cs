using ArkWallet.Domain.Exceptions;
using ArkWallet.Tests.HelpTools;
using Goal = ArkWallet.Domain.GlobalGoalContext.GlobalGoal;

namespace ArkWallet.Tests.DomainTests.GlobalGoals;

public class GlobalGoalCreationTest
{
    private static readonly TestTimeProvider Time = new();
    [Fact]
    public void CreateNew_SetsInitialStateAndFirstStep()
    {
        var goal = Goal.CreateNew(0, "Общий баланс", "Сумма балансов всех участников", 1000m, "ZZZ", 10m);

        Assert.Equal(0, goal.Id);
        Assert.Equal("Общий баланс", goal.Name);
        Assert.Equal("Сумма балансов всех участников", goal.Description);
        Assert.Equal(1000m, goal.Target);
        Assert.Equal(0m, goal.Actual);
        Assert.Equal(0m, goal.Progress);
        Assert.Equal(0, goal.AchievedCount);
        Assert.Empty(goal.History);

        var step = Assert.Single(goal.Steps);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal(1000m, step.Target);
        Assert.Equal("ZZZ", step.SymbolForReward);
        Assert.Equal(10m, step.AmountForReward);
    }

    [Fact]
    public void CreateNew_TrimsNameAndDescription()
    {
        var goal = Goal.CreateNew(0, "  Общий баланс  ", "  Описание  ", 1000m, "ZZZ", 10m);

        Assert.Equal("Общий баланс", goal.Name);
        Assert.Equal("Описание", goal.Description);
    }

    [Theory]
    [InlineData("", "Описание")]
    [InlineData("   ", "Описание")]
    [InlineData("Имя", "")]
    [InlineData("Имя", "   ")]
    public void CreateNew_EmptyNameOrDescription_Throws(string name, string description)
    {
        Assert.Throws<DomainException>(() => Goal.CreateNew(0, name, description, 1000m, "ZZZ", 10m));
    }

    [Fact]
    public void CreateNew_NullName_Throws()
    {
        Assert.Throws<DomainException>(() => Goal.CreateNew(0, null!, "Описание", 1000m, "ZZZ", 10m));
    }

    [Fact]
    public void CreateNew_NullDescription_Throws()
    {
        Assert.Throws<DomainException>(() => Goal.CreateNew(0, "Имя", null!, 1000m, "ZZZ", 10m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateNew_TargetNotPositive_Throws(decimal target)
    {
        Assert.Throws<DomainException>(() => Goal.CreateNew(0, "Имя", "Описание", target, "ZZZ", 10m));
    }

    [Fact]
    public void AddStep_AddsWithFirstStepAlreadyCreated()
    {
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);

        goal.AddStep(2, 1500m, "AAA", 5m);

        Assert.Equal(2, goal.Steps.Count);
        var step = goal.Steps.Single(s => s.StepNumber == 2);
        Assert.Equal(1500m, step.Target);
        Assert.Equal("AAA", step.SymbolForReward);
        Assert.Equal(5m, step.AmountForReward);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddStep_StepNumberNotPositive_Throws(int stepNumber)
    {
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);

        Assert.Throws<DomainException>(() => goal.AddStep(stepNumber, 1500m, "ZZZ", 10m));
    }

    [Fact]
    public void AddStep_TargetNotPositive_Throws()
    {
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);

        Assert.Throws<DomainException>(() => goal.AddStep(2, 0m, "ZZZ", 10m));
    }

    [Fact]
    public void AddStep_FirstStepAlreadyExists_Throws()
    {
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);

        Assert.Throws<DomainException>(() => goal.AddStep(1, 2000m, "AAA", 5m));
    }

    [Fact]
    public void AddStep_DuplicateStepNumber_Throws()
    {
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);
        goal.AddStep(2, 1500m, "ZZZ", 10m);

        Assert.Throws<DomainException>(() => goal.AddStep(2, 2000m, "AAA", 5m));
    }

    [Fact]
    public async Task CheckGoal_AchievedFirstStep_UsesCreationReward()
    {
        var publisher = new RecordingEventPublisher();
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "ZZZ", 10m);
        goal.SetEventPublisher(publisher);
        goal.UpdateActual(1000m);

        await goal.CheckGoal(Time);

        Assert.Equal(1, goal.AchievedCount);
        var entry = Assert.Single(goal.History);
        Assert.Equal("ZZZ", entry.SymbolForReward);
        Assert.Equal(10m, entry.AmountForReward);
    }

    [Fact]
    public async Task CheckGoal_AchievedFirstStep_NoRewardUsesEmpty()
    {
        var publisher = new RecordingEventPublisher();
        var goal = Goal.CreateNew(0, "Имя", "Описание", 1000m, "", 0m);
        goal.SetEventPublisher(publisher);
        goal.UpdateActual(1000m);

        await goal.CheckGoal(Time);

        Assert.Equal(1, goal.AchievedCount);
        var entry = Assert.Single(goal.History);
        Assert.Equal(string.Empty, entry.SymbolForReward);
        Assert.Equal(0m, entry.AmountForReward);
    }
}