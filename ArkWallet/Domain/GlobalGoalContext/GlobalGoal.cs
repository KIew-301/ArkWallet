using ArkWallet.Domain.Common;

namespace ArkWallet.Domain.GlobalGoalContext;

internal class GlobalGoal : AggregateRoot
{
    private readonly List<GlobalGoalHistory> _history = new();
    private readonly List<GlobalGoalStep> _steps = new();

    public long Id { get; }
    public string Name { get; }
    public string Description { get; }
    public decimal Target { get; private set; }
    public decimal Actual { get; private set; }
    public decimal Progress { get; private set; }
    public int AchievedCount { get; private set; }
    public IReadOnlyList<GlobalGoalHistory> History => _history;
    public IReadOnlyList<GlobalGoalStep> Steps => _steps;

    private GlobalGoal(long id, string name, string description, decimal target, decimal actual)
    {
        Id = id;
        Name = name;
        Description = description;
        Target = target;
        Actual = actual;
        Progress = ComputeProgress(target, actual);
    }

    public static GlobalGoal Create(long id, string name, string description, decimal target, decimal actual)
    {
        return new GlobalGoal(id, name, description, target, actual);
    }

    internal static GlobalGoal Load(GlobalGoalData data)
    {
        var goal = new GlobalGoal(data.Id, data.Name, data.Description, data.Target, data.Actual);
        goal.AchievedCount = data.AchievedCount;
        goal._history.AddRange(data.History);
        goal._steps.AddRange(data.Steps);
        return goal;
    }

    internal void UpdateActual(decimal newActual)
    {
        Actual = newActual;
        Progress = ComputeProgress(Target, newActual);
    }

    private void UpdateTarget(decimal newTarget)
    {
        Target = newTarget;
        Progress = ComputeProgress(newTarget, Actual);
    }

    public async Task CheckGoal(TimeProvider timeProvider)
    {
        if (Actual < Target)
            return;

        var achievedAt = timeProvider.GetUtcNow().UtcDateTime;
        var step = GetAchievedStep();

        _history.Add(new GlobalGoalHistory(achievedAt, Target, step.SymbolForReward, step.AmountForReward));
        AchievedCount++;

        await PublishAsync(new GlobalGoalAchievedEvent(
            Name, achievedAt, Target, step.SymbolForReward, step.AmountForReward));

        UpdateTarget(GetNextTarget());
    }

    private GlobalGoalStep GetAchievedStep()
    {
        var step = _steps.FirstOrDefault(s => s.StepNumber == AchievedCount + 1);
        return step ?? new GlobalGoalStep(0, Target, string.Empty, 0m);
    }

    private decimal GetNextTarget()
    {
        var nextStep = _steps.FirstOrDefault(s => s.StepNumber == AchievedCount + 1);
        if (nextStep is not null)
            return nextStep.Target;

        var lastStep = _steps.OrderByDescending(s => s.StepNumber).FirstOrDefault();
        return lastStep?.Target ?? Target;
    }

    private static decimal ComputeProgress(decimal target, decimal actual)
    {
        if (target <= 0)
            return 0;

        var progress = actual / target;
        return progress < 0 ? 0 : progress;
    }
}

internal class GlobalGoalHistory
{
    public DateTime AchievedAt { get; }
    public decimal Target { get; }
    public string SymbolForReward { get; }
    public decimal AmountForReward { get; }

    internal GlobalGoalHistory(DateTime achievedAt, decimal target, string symbolForReward, decimal amountForReward)
    {
        AchievedAt = achievedAt;
        Target = target;
        SymbolForReward = symbolForReward;
        AmountForReward = amountForReward;
    }
}

internal class GlobalGoalStep
{
    public int StepNumber { get; }
    public decimal Target { get; }
    public string SymbolForReward { get; }
    public decimal AmountForReward { get; }

    internal GlobalGoalStep(int stepNumber, decimal target, string symbolForReward, decimal amountForReward)
    {
        StepNumber = stepNumber;
        Target = target;
        SymbolForReward = symbolForReward;
        AmountForReward = amountForReward;
    }
}

internal sealed record GlobalGoalData(
    long Id,
    string Name,
    string Description,
    decimal Target,
    decimal Actual,
    int AchievedCount,
    List<GlobalGoalHistory> History,
    List<GlobalGoalStep> Steps);
