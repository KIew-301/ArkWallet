using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities;

internal class GlobalGoalStep
{
    [Key]
    public long Id { get; set; }
    public long GoalId { get; set; }
    public int StepNumber { get; set; }
    public decimal Target { get; set; }
    public string SymbolForReward { get; set; } = string.Empty;
    public decimal AmountForReward { get; set; }

    public static GlobalGoalStep Create(long goalId, int stepNumber, decimal target, string symbolForReward, decimal amountForReward)
    {
        return new GlobalGoalStep
        {
            GoalId = goalId,
            StepNumber = stepNumber,
            Target = target,
            SymbolForReward = symbolForReward,
            AmountForReward = amountForReward
        };
    }
}
