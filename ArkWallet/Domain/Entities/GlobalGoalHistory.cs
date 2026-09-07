using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities;

internal class GlobalGoalHistory
{
    [Key]
    public long Id { get; set; }
    public long GoalId { get; set; }
    public DateTime AchievedAt { get; set; }
    public decimal Target { get; set; }
    public string SymbolForReward { get; set; } = string.Empty;
    public decimal AmountForReward { get; set; }

    public static GlobalGoalHistory Create(
        long goalId, DateTime achievedAt, decimal target, string symbolForReward, decimal amountForReward)
    {
        return new GlobalGoalHistory
        {
            GoalId = goalId,
            AchievedAt = achievedAt,
            Target = target,
            SymbolForReward = symbolForReward,
            AmountForReward = amountForReward
        };
    }
}
