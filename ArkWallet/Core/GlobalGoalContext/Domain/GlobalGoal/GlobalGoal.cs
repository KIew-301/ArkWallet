using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.GlobalGoalContext.Domain.Events;

namespace ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;

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

    public static GlobalGoal CreateNew(
        long id, string name, string description, decimal target,
        string symbolForReward, decimal amountForReward)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new DomainException("Имя цели не может быть пустым");

        if (string.IsNullOrWhiteSpace(normalizedDescription))
            throw new DomainException("Описание цели не может быть пустым");

        if (target <= 0)
            throw new DomainException("Целевое значение должно быть больше нуля");

        var goal = new GlobalGoal(id, normalizedName, normalizedDescription, target, 0m);
        goal._steps.Add(new GlobalGoalStep(1, target, symbolForReward?.Trim() ?? string.Empty, amountForReward));
        return goal;
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

    /// <summary>
    /// Добавляет промежуточный шаг цели с собственной наградой.
    /// Валидация бизнес-правил шага выполняется в домене.
    /// </summary>
    public void AddStep(int stepNumber, decimal target, string symbolForReward, decimal amountForReward)
    {
        if (stepNumber <= 0)
            throw new DomainException("Номер шага должен быть больше нуля");

        if (target <= 0)
            throw new DomainException("Целевое значение шага должно быть больше нуля");

        if (_steps.Any(s => s.StepNumber == stepNumber))
            throw new DomainException($"Шаг {stepNumber} уже существует");

        _steps.Add(new GlobalGoalStep(stepNumber, target, symbolForReward?.Trim() ?? string.Empty, amountForReward));
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
